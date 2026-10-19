using System;
using CameraCaptureApp.Native;

namespace CameraCaptureApp.Services
{
    internal sealed class Lsi8181MeterWheelService : IDisposable
    {
        private bool _initialized;
        private byte _cardId;

        public byte CardId
        {
            get { return _cardId; }
        }

        public bool IsInitialized
        {
            get { return _initialized; }
        }

        public void Open(byte cardId, byte multipleRate, int compareIncrement, ushort cmpOutWidth)
        {
            Open(cardId, multipleRate, compareIncrement, cmpOutWidth, false, null);
        }

        public void Open(
            byte cardId,
            byte multipleRate,
            int compareIncrement,
            ushort cmpOutWidth,
            Lsi8181ExtensionCompareChannel[] extensionCompareChannels)
        {
            Open(cardId, multipleRate, compareIncrement, cmpOutWidth, false, extensionCompareChannels);
        }

        public void Open(
            byte cardId,
            byte multipleRate,
            int compareIncrement,
            ushort cmpOutWidth,
            bool reverseDirection,
            Lsi8181ExtensionCompareChannel[] extensionCompareChannels)
        {
            var status = Lsi8181Native.LSI8181_initial();
            EnsureSuccess(status, "Initialize LSI8181");

            _initialized = true;
            _cardId = cardId;

            ulong ioAddress = 0;
            ulong tcAddress = 0;
            status = Lsi8181Native.LSI8181_info(cardId, ref ioAddress, ref tcAddress);
            EnsureSuccess(status, "Read LSI8181 card info");

            status = Lsi8181Native.LSI8181_CI_mode_set(
                cardId,
                Lsi8181Native.QuadratureMode,
                Lsi8181Native.DebounceTime1Us,
                multipleRate);
            EnsureSuccess(status, "Set encoder input mode");

            SetReverseDirection(reverseDirection);

            status = Lsi8181Native.LSI8181_compare_mode_set(cardId, Lsi8181Native.CompareAutoIncrement);
            EnsureSuccess(status, "Set compare mode to auto increment");

            status = Lsi8181Native.LSI8181_compare_increment_set(cardId, compareIncrement);
            EnsureSuccess(status, "Set compare auto increment value");

            status = Lsi8181Native.LSI8181_compare_CMP_OUT_set(
                cardId,
                Lsi8181Native.CmpOutNormalPolarity,
                Lsi8181Native.CmpOutPulse,
                cmpOutWidth);
            EnsureSuccess(status, "Set CMP OUT pulse output mode");

            status = Lsi8181Native.LSI8181_toggle_preset(cardId, Lsi8181Native.CmpOutEnabled);
            EnsureSuccess(status, "Enable CMP OUT");

            if (extensionCompareChannels != null)
            {
                ApplyExtensionCompareChannels(extensionCompareChannels);
            }

            status = Lsi8181Native.LSI8181_counter_start(cardId, Lsi8181Native.CounterCompare);
            EnsureSuccess(status, "Start encoder counter with compare output");
        }

        public int ReadEncoder()
        {
            EnsureOpen();
            var value = 0;
            var status = Lsi8181Native.LSI8181_counter_read(_cardId, ref value);
            EnsureSuccess(status, "Read encoder counter");
            return value;
        }

        public void SetEncoder(int value)
        {
            EnsureOpen();
            var status = Lsi8181Native.LSI8181_counter_set(_cardId, value);
            EnsureSuccess(status, "Set encoder counter");
        }

        public int ReadCompare()
        {
            EnsureOpen();
            var value = 0;
            var status = Lsi8181Native.LSI8181_compare_value_read(_cardId, ref value);
            EnsureSuccess(status, "Read compare value");
            return value;
        }

        public void SetCompare(int value)
        {
            EnsureOpen();
            var status = Lsi8181Native.LSI8181_compare_value_set(_cardId, value);
            EnsureSuccess(status, "Set compare value");
        }

        public void SetCompareIncrement(int value)
        {
            EnsureOpen();
            var status = Lsi8181Native.LSI8181_compare_increment_set(_cardId, value);
            EnsureSuccess(status, "Set compare auto increment value");
        }

        public void SetMultipleRate(byte multipleRate)
        {
            EnsureOpen();
            var status = Lsi8181Native.LSI8181_CI_mode_set(
                _cardId,
                Lsi8181Native.QuadratureMode,
                Lsi8181Native.DebounceTime1Us,
                multipleRate);
            EnsureSuccess(status, "Set encoder multiple rate");
        }

        public void SetReverseDirection(bool reverseDirection)
        {
            EnsureOpen();

            ushort polarity = 0;
            var status = Lsi8181Native.LSI8181_CIO_polarity_read(_cardId, ref polarity);
            EnsureSuccess(status, "Read encoder input polarity");

            var mask = (ushort)(1 << Lsi8181Native.APhasePolarityBit);
            polarity = reverseDirection
                ? (ushort)(polarity | mask)
                : (ushort)(polarity & ~mask);

            status = Lsi8181Native.LSI8181_CIO_polarity_set(_cardId, polarity);
            EnsureSuccess(status, "Set encoder direction polarity");
        }

        public void SetCmpOutWidth(ushort outWidth)
        {
            EnsureOpen();
            var status = Lsi8181Native.LSI8181_compare_CMP_OUT_set(
                _cardId,
                Lsi8181Native.CmpOutNormalPolarity,
                Lsi8181Native.CmpOutPulse,
                outWidth);
            EnsureSuccess(status, "Set CMP OUT pulse width");

            status = Lsi8181Native.LSI8181_toggle_preset(_cardId, Lsi8181Native.CmpOutEnabled);
            EnsureSuccess(status, "Enable CMP OUT");
        }

        public Lsi8181ExtensionCompareChannel[] ReadExtensionCompareChannels()
        {
            EnsureOpen();

            byte mask = 0;
            var status = Lsi8181Native.LSI8181_compare_offset_mask_read(_cardId, ref mask);
            EnsureSuccess(status, "Read extension compare mask");

            var channels = new Lsi8181ExtensionCompareChannel[8];
            for (byte channel = 0; channel < channels.Length; channel++)
            {
                short offset = 0;
                status = Lsi8181Native.LSI8181_compare_offset_read(_cardId, channel, ref offset);
                EnsureSuccess(status, "Read CMP" + channel + " offset compare");

                ushort pulseWidth = 0;
                status = Lsi8181Native.LSI8181_compare_offset_out_width_read(_cardId, channel, ref pulseWidth);
                EnsureSuccess(status, "Read CMP" + channel + " pulse width");

                byte outputState = 0;
                status = Lsi8181Native.LSI8181_compare_offset_output_point_read(_cardId, channel, ref outputState);
                EnsureSuccess(status, "Read CMP" + channel + " output state");

                channels[channel] = new Lsi8181ExtensionCompareChannel
                {
                    Channel = channel,
                    Masked = (mask & (1 << channel)) != 0,
                    OffsetCompare = offset,
                    PulseWidth = pulseWidth,
                    OutputState = outputState != 0,
                    Status = outputState != 0
                };
            }

            return channels;
        }

        public bool[] ReadExtensionCompareStatus()
        {
            EnsureOpen();

            var statusValues = new bool[8];
            for (byte channel = 0; channel < statusValues.Length; channel++)
            {
                byte outputState = 0;
                var status = Lsi8181Native.LSI8181_compare_offset_output_point_read(_cardId, channel, ref outputState);
                EnsureSuccess(status, "Read CMP" + channel + " status");
                statusValues[channel] = outputState != 0;
            }

            return statusValues;
        }

        public void ApplyExtensionCompareChannels(Lsi8181ExtensionCompareChannel[] channels)
        {
            EnsureOpen();
            if (channels == null || channels.Length != 8)
            {
                throw new ArgumentException("Exactly 8 extension compare channels are required.", "channels");
            }

            byte mask = 0;
            for (byte channel = 0; channel < channels.Length; channel++)
            {
                var channelSettings = channels[channel];
                var status = Lsi8181Native.LSI8181_compare_offset_set(_cardId, channel, channelSettings.OffsetCompare);
                EnsureSuccess(status, "Set CMP" + channel + " offset compare");

                status = Lsi8181Native.LSI8181_compare_offset_out_width_set(_cardId, channel, channelSettings.PulseWidth);
                EnsureSuccess(status, "Set CMP" + channel + " pulse width");

                status = Lsi8181Native.LSI8181_compare_offset_output_point_set(
                    _cardId,
                    channel,
                    channelSettings.OutputState ? (byte)1 : (byte)0);
                EnsureSuccess(status, "Set CMP" + channel + " output state");

                if (channelSettings.Masked)
                {
                    mask = (byte)(mask | (1 << channel));
                }
            }

            var maskStatus = Lsi8181Native.LSI8181_compare_offset_mask_set(_cardId, mask);
            EnsureSuccess(maskStatus, "Set extension compare mask");
        }

        public void Close()
        {
            if (!_initialized)
            {
                return;
            }

            try
            {
                Lsi8181Native.LSI8181_counter_stop(_cardId);
            }
            finally
            {
                Lsi8181Native.LSI8181_close();
                _initialized = false;
            }
        }

        public void Dispose()
        {
            Close();
        }

        private void EnsureOpen()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("LSI8181 meter wheel card is not connected.");
            }
        }

        private static void EnsureSuccess(uint status, string action)
        {
            if (status != Lsi8181Native.Success)
            {
                throw new InvalidOperationException(action + " failed. Status: " + status);
            }
        }

    }

    internal sealed class Lsi8181ExtensionCompareChannel
    {
        public byte Channel { get; set; }

        public bool Masked { get; set; }

        public short OffsetCompare { get; set; }

        public ushort PulseWidth { get; set; }

        public bool OutputState { get; set; }

        public bool Status { get; set; }
    }
}
