using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntegratedImageProcessingApp.Services
{
    /// <summary>
    /// Shared CPU worker budget for application-owned Parallel.For loops.
    /// Nested loops run inline so ROI/core/object parallelism cannot multiply.
    /// </summary>
    public static class InspectionParallelism
    {
        private static readonly object Sync = new object();
        private static readonly ThreadLocal<int> WorkerDepth = new ThreadLocal<int>();
        private static int workerLimit = GetAutomaticWorkerLimit();
        private static int activeWorkers;

        public static int WorkerLimit
        {
            get { lock (Sync) return workerLimit; }
        }

        public static int ActiveWorkers
        {
            get { lock (Sync) return activeWorkers; }
        }

        public static void Configure(int requestedWorkerLimit)
        {
            int nextLimit = requestedWorkerLimit <= 0
                ? GetAutomaticWorkerLimit()
                : Math.Max(1, Math.Min(32, requestedWorkerLimit));
            lock (Sync)
            {
                workerLimit = nextLimit;
                Monitor.PulseAll(Sync);
            }
        }

        public static void For(
            int fromInclusive,
            int toExclusive,
            ParallelOptions options,
            Action<int> body)
        {
            if (body == null) throw new ArgumentNullException("body");
            ParallelOptions sourceOptions = options ?? new ParallelOptions();
            CancellationToken cancellationToken = sourceOptions.CancellationToken;

            // If a processing worker enters another parallel stage, run that stage on
            // the current worker. This avoids nested worker multiplication and deadlocks.
            if (WorkerDepth.Value > 0 || toExclusive - fromInclusive <= 1)
            {
                for (int index = fromInclusive; index < toExclusive; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    body(index);
                }
                return;
            }

            int requested = sourceOptions.MaxDegreeOfParallelism;
            int limit = WorkerLimit;
            int degree = requested < 1 ? limit : Math.Min(requested, limit);
            var boundedOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, degree),
                TaskScheduler = sourceOptions.TaskScheduler
            };

            Parallel.For(fromInclusive, toExclusive, boundedOptions, index =>
            {
                EnterWorker(cancellationToken);
                int previousDepth = WorkerDepth.Value;
                WorkerDepth.Value = previousDepth + 1;
                try
                {
                    body(index);
                }
                finally
                {
                    WorkerDepth.Value = previousDepth;
                    ExitWorker();
                }
            });
        }

        private static void EnterWorker(CancellationToken cancellationToken)
        {
            lock (Sync)
            {
                while (activeWorkers >= workerLimit)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Monitor.Wait(Sync, 50);
                }
                activeWorkers++;
            }
        }

        private static void ExitWorker()
        {
            lock (Sync)
            {
                if (activeWorkers > 0) activeWorkers--;
                Monitor.PulseAll(Sync);
            }
        }

        private static int GetAutomaticWorkerLimit()
        {
            return Math.Max(1, Math.Min(8, Environment.ProcessorCount));
        }
    }
}
