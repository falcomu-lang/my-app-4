# Agent Instructions — Integrated Image Processing App

## Project identity

- Windows desktop grayscale line-scan inspection software.
- Solution: `MyApp4.sln`.
- Main application: `IntegratedImageProcessingApp` (WinForms, .NET Framework 4.7.2, x64).
- Camera module: `CameraCaptureModule` (WinForms library, .NET Framework 4.7.2, x64; Sapera SDK and LSI meter-wheel integration).
- Image processing uses OpenCvSharp. The camera SDK and LSI native libraries are vendor dependencies; do not replace, emulate, or change their calling conventions without a specific task.
- Read [README.md](README.md) for the product overview and [PROJECT_HANDOFF.md](PROJECT_HANDOFF.md) for workflow, file responsibilities, build setup, and risks. The `docs/` directory contains the operator manual, specification, architecture notes, validation guidance, and the code review.

## Rules for making changes

1. Start with `git status --short` and inspect the relevant source and its callers before editing. The working tree may contain user or agent changes. Preserve them; never reset, clean, or overwrite unrelated work.
2. This is an older non-SDK C# project. Every new `.cs` file must be added to the relevant `.csproj` `<Compile Include="..." />` list. Check both projects when changing shared camera-facing contracts.
3. Keep image data grayscale (`Mono8`) through acquisition and processing. The camera inspection path should process the in-memory `CameraGrayscaleFrame`/`LargeImageSource`; saving a TIFF is a parallel archival path, never an intermediate input step.
4. Camera frames are not queued for inspection while a previous frame is busy. Preserve the explicit skip-when-busy behavior unless the request changes the acquisition policy; keep skipped-frame status observable.
5. Protect large-image and `Bitmap` lifetimes across worker/UI boundaries. For every acquired `LargeImageSource`, `Mat`, `Bitmap`, frame lease, and snapshot, make ownership and disposal explicit on both success and failure paths.
6. Inspection results are per object/part. Do not collapse all objects in a captured image into one NG result. Keep size, defect, and combined judgement meanings consistent across the result-review UI, overview, history, and TCP payload.
7. Preserve stable IDs for recipes, ROI definitions/groups, inspection records, stations, and results. Do not use display names or list positions as persistent identity.
8. MainForm is split into partial files but still shares substantial mutable state. Follow the existing responsibility split; avoid adding unrelated logic to `MainForm.cs`. Extract a service only when its inputs/outputs and ownership can be made clear.
9. When editing settings or history formats, preserve existing files and provide backward-compatible defaults/migration. Configuration is stored under the executable directory; do not silently replace invalid user settings with defaults.
10. Avoid changes to result algorithms, trigger mode, queue policy, file retention, or wire protocol semantics outside the requested scope. Call out any behavior change and its compatibility impact.
11. Do not add or run tests unless requested. A Debug build is appropriate after code changes. Hardware behavior, detection quality, sustained throughput, and multi-PC networking require explicit on-device/on-image validation; a successful build does not prove those behaviors.
12. Do not commit or push unless explicitly requested.

## Current architecture and data flow

```text
Sapera camera -> Mono8 CameraGrayscaleFrame -> in-memory LargeImageSource
  -> active Recipe and object-finding pipeline
  -> per-object size judgement + flat-field/conditions 1–6/frequency 1–2
  -> defect integration -> per-object combined judgement
  -> inspection overview and optional history record
```

- One app instance controls one local camera. The connection-controller page can monitor multiple configured station IDs.
- Camera processing is independent of TIFF loading: the live grayscale buffer is handed to the inspection path, while an archival TIFF is written separately in the background.
- Image handling is memory-intensive. Avoid full-frame copies when a tile/ROI operation can preserve the same semantics. Application-owned `Parallel.For` loops use `InspectionParallelism`; nested calls run inline and the configured worker cap bounds active loop bodies. This does not cap OpenCV native threads, camera-save workers, total CPU use, or image-cache RAM.
- The “frequency anomaly” implementation is windowed spatial-domain energy scanning and optional neighboring-window comparison; it is not FFT/DFT. Texture anomaly and automatic directional-line analysis were removed.
- History uses daily SQLite files. The historical rerun must use the stored parameter identity/snapshot; do not guess between recipes or parameters when identity is ambiguous.
- Full navigation, UI ownership, and processing orchestration remain coupled to `MainForm` partial classes. Treat refactoring as a behavior-preserving change and keep the current page/result retention semantics.

## TCP/IP result delivery status (2026-11-04)

- Existing `station-status` messages remain for compatibility.
- A peer advertises reliable-result support in `hello`. The new `inspection-result` message contains a result ID and per-object judgement summary.
- The sender persists pending messages under `tcp-results/outbox`, retries them while a compatible peer is connected, and removes them only after `result-ack`.
- The receiver writes each result under `tcp-results/inbox/yyyyMMdd` before sending its ACK. Repeated result IDs map to the same stored record. A delayed older result is archived and ACKed without replacing a newer result in the live station card.
- Optional command authentication uses HMAC-SHA256. When the controlled station enables `RequireAuthenticatedCommands`, the controller must use the matching station `CommandSecret`; signatures cover command ID/name, source/target station IDs, and UTC timestamp. The ±2 minute timestamp check requires synchronized clocks, and accepted IDs are persisted under `tcp-results/accepted-commands/yyyyMMdd` to reject replay.
- A Debug build of `IntegratedImageProcessingApp.csproj` succeeded on 2026-11-04 after these changes. No automated test project was run, and result delivery has not been validated across physical machines or forced disconnect/restart scenarios.
- Important remaining limitations: command authentication is opt-in; secrets are stored as plaintext in local config; there is no TLS; station-status, inspection-result, and ACK messages are not signed; the reliable-result outbox is shared by peers and has no retention cleanup. Role passwords are local UI controls and are not TCP credentials. Do not describe the whole network path as authenticated or secure.
- The code review report predates some recent changes and is not a current pass/fail checklist. Verify each cited finding against current source before acting on it.

## Build and dependency notes

- Build the solution with Visual Studio/MSBuild and the .NET Framework 4.7.2 Developer Pack.
- The project files use explicit compile lists. Local Sapera and LSI SDK paths can be overridden with `CameraSdkDirectory` and `LsiNativeDirectory` MSBuild properties; do not commit machine-specific SDK binaries or credentials.
- Typical command from the repository root:

```powershell
msbuild .\MyApp4.sln /t:Build /p:Configuration=Debug '/p:Platform=Any CPU'
```

- If the solution build cannot resolve a local vendor SDK, verify the configured SDK paths before changing application references.
- There is no dedicated automated test project in the solution. Report exactly what was built or manually verified.

## Documentation maintenance

- Update `README.md` for user-facing product overview changes.
- Update `PROJECT_HANDOFF.md` for architecture, file ownership, workflow, build, and current implementation status changes.
- Update the relevant specification/validation document under `docs/` when a behavior or protocol contract changes.
- Keep claims evidence-based: distinguish implemented, built, manually verified, and hardware/network validated.
