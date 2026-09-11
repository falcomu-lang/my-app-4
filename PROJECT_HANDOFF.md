# Integrated Image Processing App Handoff

## Project
- Path: `C:\Users\falcomu\Documents\Codex\程式撰寫 專案資料夾\整合式影像處理軟件\my-app-4`
- GitHub: `https://github.com/falcomu-lang/my-app-4`
- Type: C# Windows Forms app, .NET Framework 4.7.2
- Solution: `MyApp4.sln`
- Main project: `IntegratedImageProcessingApp\IntegratedImageProcessingApp.csproj`

## Current UI Direction
- The main form is intended to remain editable in the Visual Studio WinForms Designer.
- Main layout is roughly 1:3:1:
  - Left: function list.
  - Center: image viewing area.
  - Right: parameter/settings area.
- Center image area is split into left and right viewers.
- Each side has tabs:
  - `原圖`
  - `處理後`
  - `物件結果`
  - `debug`
- Runtime image display uses `ImageDisplayControl`; designer-visible placeholder panels are kept in `MainForm.Designer.cs` so the form remains visually editable.

## Completed Features
- `讀取圖片`
  - Left function list item opens an `OpenFileDialog`.
  - Supported image filters: BMP, JPG/JPEG, PNG, TIF/TIFF, all files.
  - Images are checked when loaded. If already grayscale, they are used directly.
  - If a user accidentally selects a color image, it is converted to grayscale in memory before display; no temporary image file is saved and reloaded.
  - Selected image is loaded into both left and right `原圖` tabs.
  - Both sides automatically switch to `原圖`.

  - Image viewer interaction
    - Mouse wheel zoom.
    - Left-button drag pan.
    - Fit/reset view button.
    - Fit/reset emits a dedicated sync event so pressing reset on either visible side resets the opposite visible side too.
  - Status text shows zoom, offset, and image coordinate information.
  - Behavior is modeled after the viewer interaction in:
    `C:\Users\falcomu\Documents\Codex\程式撰寫 專案資料夾\攝影機影像擷取\my-app-2`
  - Large image display now reuses the tiled rendering approach from `my-app-2`:
    - `LargeImageSource` loads huge files through WIC.
    - WIC decoding must use `BitmapCacheOption.OnDemand`; `OnLoad` can throw `OutOfMemoryException` during decoder creation for images such as `16384 x 50000`.
    - Preview generation uses `BitmapImage.DecodePixelWidth/DecodePixelHeight` and copies pixels directly into `LockBits`; avoid `TransformedBitmap.CopyPixels` with a huge intermediate byte array because it can still throw `OutOfMemoryException` on very large files.
    - `ImageDisplayControl.LoadImageFromFileAsync` reads dimensions with WIC first. Huge images must go directly to `LargeImageSource`; do not call GDI+ `Image.FromStream` just to inspect size because it can throw `ArgumentException` before the tiled path starts.
    - Low zoom paints a progressive preview.
    - Higher zoom requests and draws `1024 x 1024` tiles on demand. Tile rendering begins at about `0.08x`, so `0.1x`/`0.12x` should sharpen once tiles finish loading instead of staying on the preview forever.
    - While panning at `0.08x` or higher, the active viewer still draws cached high-resolution tiles and queues missing tiles. This keeps the dragged viewer from becoming blurrier than the synced opposite viewer at `0.1x`-`0.19x`.
    - Panning invalidation is throttled to roughly 16 ms to reduce uneven drag feel between left/right viewers.
    - Tile drawing uses destination rounding and `WrapMode.TileFlipXY` to avoid visible seams.
    - Huge image files are loaded into one shared `LargeImageSource` for the left/right original viewers. Processed viewers also reference that same source, so the app does not open/decode the same huge image separately for each side/tab.
    - `LargeImageSource` uses reference counting through `AddReference`/`ReleaseReference`; do not dispose a shared source directly from a control.
  - Large-image processed previews use the same tiled display path:
    - Processed viewers load the original large image through `LargeImageSource`.
    - The viewer paints original image preview/tiles first.
    - `MainForm` handles `LargeImageOverlayPaint` to draw cached overlay tiles and queue missing ROI-intersecting tiles for background calculation.
    - Do not compute edge masks synchronously inside Paint; doing so can freeze the UI when switching to the `處理後` tab.
    - Large processed overlay tiles are `128 x 128` and queue at most 2 background tile calculations at once so large ROIs do not flood the thread pool.
    - `LargeImageSource.CreateRegionBitmap` must not hold the shared source lock while WIC decodes a region; otherwise overlay calculation can block original image tile refresh and appear stuck.
    - Processed overlay calculation should prefer `LargeImageSource.TryCreateRegionBitmapFromCachedTile`. It crops from already-cached `1024 x 1024` source tiles instead of asking WIC to decode many tiny overlay regions. If the source tile is missing, queue the source tile and retry on repaint.
    - If the high-resolution source tile is not cached yet, processed overlay falls back to `LargeImageSource.GetBestPreview(0f)` so the `處理後` tab does not stay blank with an endless pending count. When the high-resolution tile finishes loading through `QueueTile`, the preview overlay cache is invalidated and recalculated.
    - Current preferred large-image processed preview flow: build one ROI-level boolean mask for the selected processing step, then render red overlay tiles from that mask. The display path should not be responsible for running the edge algorithm per visible tile; pan/zoom should only redraw from the prepared mask.
    - ROI mask creation is chunked (`512 x 512`) inside the selected ROI. Do not create one full ROI bitmap for large images; each chunk is processed and copied back into the ROI-level mask, with progress shown as `ROI Mask n/total`.
    - Overlay generation uses `LockBits` batch access for grayscale extraction and red transparent overlay creation; avoid `GetPixel`/`SetPixel` in this path because it makes large-image processing appear stuck.
    - Overlay tile drawing validates destination/source rectangles and catches GDI+ `ArgumentException` so one invalid overlay draw does not crash painting.
    - Overlay cache reads validate that cached `Bitmap` instances are still usable; disposed/invalid entries are removed and recalculated to avoid GDI+ `ArgumentException` during Paint.
    - Red result overlays are cached as transparent tile bitmaps and cleared when method/parameter/ROI changes.

- Visible left/right view synchronization
  - Zoom/pan/reset sync between the currently visible left and right image controls.
  - The app keeps a shared image view state. When either left or right tab changes, the newly visible image control applies the same zoom/pan state so left/right image scale and position remain exactly aligned.
  - Hidden tabs are not refreshed unless needed, but when they become visible they must match the shared zoom/pan state.
  - Updating processed previews must preserve and reapply this shared zoom/pan state.
  - Programmatic processed-image replacement is wrapped with the view-sync guard. This prevents a first-time processed tab update from firing `ViewChanged` after its internal fit and overwriting the user's shared zoom/pan state.
  - Viewer size/page changes do not auto-fit an already-loaded image. `ImageDisplayControl_SizeChanged` only invalidates/repaints, so switching tabs or layout pages should not reset zoom or pan. Fit-to-view is reserved for loading a new image, replacing with a different-size image, or the user pressing the reset/fit button.

- `指定 ROI`
  - Left function item expands/collapses child items at runtime:
    - `新增 ROI`
    - `ROI 1`, `ROI 2`, `ROI 3`, ...
  - `新增 ROI` enables ROI drawing on currently visible `原圖` viewers that already have an image.
  - User drags a green rectangle on left or right `原圖`.
  - When mouse is released, a confirmation dialog asks whether to keep the ROI.
  - Confirmed ROI is appended to the ROI list and stored in image coordinates.
  - Selecting `ROI N` makes it the active ROI, shows its green overlay on the right `原圖`, and makes image processing previews use that ROI.
  - Selecting `ROI N` expands a child command:
    - `刪除`
  - `刪除` asks for confirmation before removing that ROI.
  - ROI list state is persisted in `SystemParameters.ini` under `[ROIs]`; legacy single `[ROI]` values are still loaded and converted into `ROI 1`.

- `影像處理`
  - Left function item expands/collapses child items at runtime.
  - Initial child item is `新增影像處理`; there are no processing steps until the user adds them.
  - Each click on `新增影像處理` appends a new processing step shown as `處理N(未決定)` when no algorithm has been selected yet.
  - Selecting a processing step expands `刪除`, `上移`, and `下移`.
  - `刪除` asks for confirmation with `是否要刪除該項處理？` before removing the step.
  - Selecting a processing step also shows a right-side runtime `TreeView` for the expected image processing flow.
  - The flow tree currently contains these top-level categories:
    - `Edge Detection`
    - `Threshold`
    - `Morphology`
    - `Contour Analysis`
    - `Feature Filter`
    - `Object Selector`
    - `Object Result`
  - Clicking a flow tree node is treated as the accepted method for the selected processing step, saves it to INI, and updates the left menu display from `處理N(未決定)` to `處理N(MethodName)`.
  - Edge Detection methods currently have right-side parameter panels:
    - `Polarity Edge`: `Polarity`, `ContrastThreshold`, `EdgeWidth`, `Smoothing`, `SearchDirection`, `EdgeSelection`, `SubPixel`, `MinEdgeLength`, `MaxGap`.
    - `Canny Edge`: `LowThreshold`, `HighThreshold`, `KernelSize`, `L2Gradient`, `GaussianBlurSize`, `GaussianSigma`, `EdgeSelection`, `MinEdgeLength`, `MaxGap`.
    - `Sobel Edge`: `Direction`, `KernelSize`, `Scale`, `Delta`, `OutputMode`, `Threshold`, `EdgeSelection`, `MinEdgeLength`, `MaxGap`.
  - Kernel-size options are shared by Canny/Sobel-related controls: `3`, `5`, `7`, `9`, `11`, `13`, `15`.
  - Selecting one of those methods switches the right side from the flow tree to its parameter editor. The `重新選擇方法` button returns to the flow tree.
  - Parameter edits are saved immediately into each step's `StepN.Parameters` INI field.
  - Numeric parameters use `NumericUpDown` controls. Mouse wheel adjusts values directly; holding Shift increases the step size by 10x.
  - Processing previews run only inside the saved ROI, but the `處理後` image remains full-frame. Pixels outside ROI stay as the original grayscale image; detected `true` pixels inside ROI are drawn in red.
  - The green ROI overlay is also shown on processed images so users can see exactly where the operation was applied.
  - Processed previews are resource-conscious:
    - Parameter/method/ROI changes mark the processed image as dirty.
    - If a left or right `處理後` tab is visible, updates are debounced by 200 ms and computed once.
    - Only visible `處理後` tabs are refreshed immediately.
    - If no `處理後` tab is visible, the result is computed later when the user switches to a `處理後` tab.
    - Updating a processed preview preserves the user's current zoom/pan when the new result has the same image size.
    - If there is no previewable image-processing step left, both processed viewers are cleared so stale red overlays are never shown.
    - While processing preview is being computed, the lower status bar shows `影像處理運算中...`.
    - In large-image mode, `處理後` does not create a full-size processed bitmap. It renders `LargeImageSource` base tiles plus ROI-local red mask overlay tiles.
  - Current preview implementations use simple in-app masks for Edge Detection methods. `Polarity Edge`, `Canny Edge`, and `Sobel Edge` all display red edge overlays, and their visible UI parameters are connected to preview calculation.
  - Canny `GaussianBlurSize`/`GaussianSigma` and Polarity `Smoothing` flow through `ApplyGaussianBlur`, which uses Gaussian distance weighting.
  - Processing step numbers are display positions only. When deleting or moving steps, the visible `處理1`, `處理2`, `處理3` numbering is regenerated, but each step's underlying method/parameters move with that step.
  - Processing workflow state is persisted in `SystemParameters.ini` under `[ImageProcessing]` so future algorithm selections can be restored and reordered safely.

## System Parameters
- Runtime settings are saved to an INI text file:
  - `SystemParameters.ini`
  - Location: application base/output directory, next to the running exe.
- Current saved fields:

```ini
[System]
LastImagePath=

[ROI]
Enabled=false
X=0
Y=0
Width=0
Height=0

[ImageProcessing]
Count=0
Step1.Method=
Step1.Parameters=
```

- On startup:
  - If `LastImagePath` exists, the app reloads the previous image into both `原圖` viewers.
  - If ROI is enabled and valid, the right `原圖` restores the ROI overlay.
  - If image processing steps are saved and at least one step is previewable, the app prepares the `處理後` image from the saved ROI/method/parameters so both left/right processed tabs have an image ready after startup.

## Important Files
- `IntegratedImageProcessingApp\Forms\MainForm.cs`
  - Main menu behavior.
  - Image load flow.
  - Visible viewer sync.
  - ROI workflow.
  - Image processing step list, reorder/delete actions, and right-side flow tree selection.
  - Startup restore from INI.

- `IntegratedImageProcessingApp\Forms\MainForm.Designer.cs`
  - Designer-editable main frame and placeholder controls.
  - Keep this file friendly to Visual Studio Designer.

- `IntegratedImageProcessingApp\Controls\ImageDisplayControl.cs`
  - Runtime image loading.
  - Ensures incoming images are grayscale so future analysis can assume grayscale input. Already-grayscale images are not converted again.
  - Exposes current image cloning and color display-image setting for processed red overlay previews, with optional view preservation.
  - Uses `LargeImageSource` for huge image files so the viewer can paint preview/tile regions instead of creating one massive `Bitmap`.
  - Viewer paint, zoom, pan, fit view.
  - ROI selection and ROI overlay drawing.
  - View sync event/state.

- `IntegratedImageProcessingApp\Controls\LargeImageSource.cs`
  - Ported from `C:\Users\falcomu\Documents\Codex\程式撰寫 專案資料夾\攝影機影像擷取\my-app-2\CameraCaptureApp\Controls\LargeImageSource.cs`.
  - WIC-backed preview and tile source for huge images.

- `IntegratedImageProcessingApp\Services\SystemParameterIniService.cs`
  - Simple INI load/save service.

- `IntegratedImageProcessingApp\Services\SystemParameterSettings.cs`
  - Settings model for image path and ROI.
  - Also stores image processing workflow steps, including each step's future method and parameters.

## Latest Commits
- `24e6a88 Persist ROI settings to ini`
- `17199a5 Add ROI selection workflow`
- `086e836 Sync visible image viewer state`
- `df6e005 Add image viewer pan and zoom`
- `ca47f03 Add image loading function option`

## Verification
- Last functional build check used:

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe' .\MyApp4.sln /p:Configuration=Debug /p:Platform="Any CPU" /p:OutDir=.\codex-build\ /v:minimal
```

- The temporary `IntegratedImageProcessingApp\codex-build` output directory was removed after verification.

## Notes For Next Work
- Be careful with `MainForm.Designer.cs`; avoid helper method calls or custom-control declarations in the main designer file if Visual Studio Designer starts failing.
- Prefer adding runtime behavior in `MainForm.cs` and reusable viewer behavior in `ImageDisplayControl.cs`.
- Treat images inside the app as grayscale-only. Color input files should be converted in memory at the boundary before display or analysis; already-grayscale input should not be converted again.
- There may be Visual Studio formatting-only changes in `MainForm.Designer.cs` or `MainForm.resx` after opening the designer. Inspect before committing.
