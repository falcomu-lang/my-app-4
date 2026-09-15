# Integrated Image Processing App Handoff

## Project
- Path: `C:\Users\falcomu\Documents\Codex\程式撰寫 專案資料夾\整合式影像處理軟件\my-app-4`
- GitHub: `https://github.com/falcomu-lang/my-app-4`
- Type: C# Windows Forms app, .NET Framework 4.7.2
- Solution: `MyApp4.sln`
- Main project: `IntegratedImageProcessingApp\IntegratedImageProcessingApp.csproj`

## Current Implementation Status (2026-09-15)

### Current file split status
- `Forms\MainForm.ImageProcessing.cs` contains the image-processing execution entry points and the OpenCV Canny, Sobel, and Polarity implementations.
- `Forms\MainForm.Preprocessing.cs` now contains the preprocessing fields and result types, menu/group management, method selection tree, parameter controls, OpenCV preprocessing operations, preprocessing execution, cache invalidation, ViewState restoration, timing updates, and the background update core.
- `Forms\MainForm.Relation.cs` has been added and is included by the project. It currently contains the relation-specific fields, `RelationChoice` type, and menu constant. Relation UI and execution methods remain in `MainForm.cs` and are the next relation split step.
- `MainForm.cs` still coordinates the main form, image loading, ROI, relation workflow, processed overlays, and shared display behavior. `MainForm.Designer.cs` has existing designer changes and must not be reverted or included in unrelated commits.
- The current working tree has been built successfully after the preprocessing split and relation file setup. Generated `codex-build` output is temporary and should not be committed.

### OpenCV image preprocessing pipeline
- `影像前處理` is fully implemented with OpenCV and is applied to the full grayscale image before ROI edge detection. The active order is: `原圖全影像 -> 前處理全影像 -> ROI 內邊緣偵測 -> 全圖紅色結果疊圖`.
- Implemented preprocessing methods are `Normalize`, `Gaussian Blur`, `CLAHE`, `Median Blur`, `Sharpen` (unsharp mask), and `Bilateral Filter`. Every exposed parameter is used by the OpenCV operation.
  - `Sharpen` has `Amount`, `KernelSize`, and `Sigma`. `Sigma=0` deliberately uses OpenCV's automatic value derived from the kernel size.
  - `Bilateral Filter` uses `Diameter`, `SigmaColor`, and `SigmaSpace`; its UI must call the first parameter `直徑`, not a generic core size.
- Large-image preprocessing returns a full-resolution 8-bit grayscale `Cv.Mat`. It is displayed through a memory-backed `LargeImageSource`, not via a temporary output file or a downscaled preview. The preprocessed output becomes the source for Canny/Polarity/Sobel ROI calculations.
- The `前處理` tabs display the complete preprocessed image. Parameter changes are versioned and background work that becomes stale is discarded rather than published.
- Preprocessing steps may be Ctrl-selected and grouped. A preprocessing group is sequential: its children execute from top to bottom and each output Mat is the next child's input. This differs from image-processing groups, where descendant edge steps independently process the source ROI and their result masks are combined for display.
- Preprocessing groups, step `GroupId`, names, order, methods, and parameters are persisted in `[ImagePreprocessing]` in `SystemParameters.ini`. Use `GroupCount`, `GroupN.Id`, `GroupN.ParentGroupId`, `GroupN.DisplayName`, and `StepN.GroupId`.
- A completed preprocessing step writes its measured OpenCV time next to the left-menu item, e.g. `前處理1(CLAHE) - 48 ms`. These values are runtime-only and clear when the preprocessing configuration changes.
- Any preprocessing or edge-parameter change must preserve the current `Zoom + Offset` on all image tabs. The saved view state is reapplied after the base source or display content changes; do not reintroduce a fit-to-view during parameter updates.

### Large-image tile ownership and concurrency
- `LargeImageSource` owns cached Tile bitmaps. `ImageDisplayControl` must draw only a cloned Tile and dispose that clone immediately after painting. Never expose a cache-owned GDI+ Bitmap directly to paint code: concurrent preview composition, cache eviction, or `Clone()` otherwise throws `InvalidOperationException` (`物件目前正在使用` / `其他地方正在使用物件`).
- Background tile and preview tasks hold a temporary `LargeImageSource` reference until their work completes. A rapid parameter edit can replace an old memory-backed preprocessing source, but that source must not dispose until its already queued work has left safely.

### Large-image data and processing architecture
- The application supports large grayscale images such as `16384 x 16384` and larger without creating a full-size GDI+ `Bitmap` for display.
- Debug and Release builds use `AnyCPU` with `Prefer32Bit=false`, so a 64-bit Windows system runs the application as a 64-bit process. Do not re-enable 32-bit preference: a large full-frame OpenCV grayscale `Cv.Mat` requires one contiguous native allocation before its algorithm temporaries are created.
- Display and processing are deliberately separate:
  - `LargeImageSource` is the WIC-backed display source. Both left/right viewers and all image tabs share the same source instance and request preview/tile data only when needed.
  - Large-image Canny, Polarity Edge, and Sobel Edge use one cached OpenCV grayscale source `Cv.Mat`, read once from `LargeImageSource.FilePath`. Each ROI is a lightweight OpenCV sub-matrix header sharing that native buffer; it is not a full ROI clone.
  - The result is a full-resolution binary OpenCV `Cv.Mat` for each processing cache key. The red overlay is generated only for the currently visible source rectangle, so pan/zoom does not re-run the algorithm.
- Saved ROIs are prewarmed in the background when a saved image is restored. The lower status bar shows `正在產生ROI的影像準備` during this work, then reports `ROI 處理時間` and the prepared ROI count.
- Native full-image jobs are serialized by `largeNativeProcessingGate`. This avoids several expensive OpenCV calculations running concurrently after fast parameter edits. An in-flight native OpenCV call cannot be cancelled midway; a superseded result is discarded after it returns and the newest request runs next.
- Do not add a silent fallback from the native OpenCV large-image path to the old managed/tile ROI processing path. A native load failure must be reported explicitly, otherwise a slow fallback looks like an endless calculation and is very difficult to diagnose.
- The current processing source is the full-image preprocessing output when at least one valid preprocessing step exists; otherwise it is the original grayscale image.

### Implemented edge detectors
- **Canny Edge** uses OpenCV `GaussianBlur -> Canny` and stores a native binary `Cv.Mat` result. Supported parameters are `LowThreshold`, `HighThreshold`, `KernelSize` (`3`, `5`, `7`), `L2Gradient`, `GaussianBlurSize`, and `GaussianSigma`.
- **Polarity Edge** uses the native OpenCV path: Gaussian blur, Sobel gradients, contrast comparison, and binary mask composition. Its supported parameters are `Polarity`, `ContrastThreshold`, `CoreWidth`, `Smoothing`, `GaussianSigma`, `SearchDirection`, and `BorderType`.
  - The INI key is `CoreWidth`; legacy `EdgeWidth` is read for compatibility but is removed when parameters are saved again.
  - `CoreWidth` means the Sobel derivative aperture/core size, not the width of a line to find.
  - `EdgeSelection`, `SubPixel`, `MinEdgeLength`, and `MaxGap` were intentionally removed. They are contour/feature-filter concerns and previously caused very slow connected-component work in the edge detector.
- **Sobel Edge** also uses the native OpenCV `Cv.Mat` path. Its supported parameters are `Direction`, `KernelSize` (`1`, `3`, `5`, `7`), and `Threshold`.
  - Legacy scale/delta/output/edge-selection/length/gap settings are not exposed because they are not needed for this raw binary edge stage.
- For aperture `7`, the native code scales its `CV_16S` gradient before comparison so Sobel values do not overflow.

### UI and pipeline direction
- Both viewer sides have static designer-owned tabs in this order: `原圖 -> 前處理 -> 處理後 -> 物件結果 -> debug`.
- The `前處理` tab renders the full-resolution OpenCV preprocessing result through its own memory-backed tile source and has the same zoom/pan/reset synchronization as every other tab.
- The left function menu contains `影像前處理` above `影像處理`, including named sequential groups and runtime timing labels.
- Intended final pipeline: `原圖 -> 影像前處理 -> 邊緣偵測 -> Threshold/Morphology -> Contours -> Feature Filter -> Object Selector -> Object Result`.
- The full-resolution preprocessing output is the base image of `前處理` and the processing source for edge detection; `處理後` keeps the original-image base plus ROI-local red result overlay so the original reference remains visually available.
- Each processing step displays its measured algorithm time after a successful real calculation, for example `處理1(Canny Edge) - 475 ms`.
  - This is runtime-only information and is deliberately not written to `SystemParameters.ini`; a newly started application must not display stale timing.
  - Cache hits and viewport redraws do not add or replace the measured processing time.
  - Method, parameter, or ROI changes invalidate result masks and clear all affected timing labels.
  - The time suffix is presentation only. Every left-menu processing-step parser must still recognize `處理N(Method) - N ms` as a normal processing step. This applies equally to Canny Edge, Polarity Edge, and Sobel Edge, so the step must still open its right-side parameters and refresh its cached red overlay.

### Remaining verification and next work
- Verify the preprocessing group right-click workflow on production images: Ctrl-select at least two steps, group, rename, reorder, ungroup, then confirm the final image matches the displayed group order.
- Test the latest native Polarity and Sobel paths on production-size images and parameter changes. Do not claim instant processing until that has been measured on the target images.
- Future edge methods worth considering after the current workflow is stable: Scharr, Laplacian/LoG, and later non-maximum suppression. Contour filtering remains a separate later stage.

## Current UI Direction
- The main form is intended to remain editable in the Visual Studio WinForms Designer.
- Main layout is roughly 1:3:1:
  - Left: function list.
  - Center: image viewing area.
  - Right: parameter/settings area.
- Center image area is split into left and right viewers.
- Each side has tabs:
  - `原圖`
  - `前處理`
  - `處理後`
  - `物件結果`
  - `debug`
- Runtime image display uses `ImageDisplayControl`; designer-visible placeholder panels are kept in `MainForm.Designer.cs` so the form remains visually editable.

## Architecture Split Progress
- Completed preprocessing partial split:
  - preprocessing fields and result models
  - preprocessing list/group UI and group operations
  - preprocessing method tree and parameter editor
  - OpenCV preprocessing methods and full-image execution
  - background update, generation checks, cache replacement, timing, and ViewState restore
- Started relation partial split:
  - `Forms\MainForm.Relation.cs` exists and is included in `IntegratedImageProcessingApp.csproj`
  - relation fields and `RelationChoice` model have been moved
- Remaining planned splits:
  1. Move relation parameter panel, source/target selection, apply, list rebuild, and relation context menu methods to `MainForm.Relation.cs`.
  2. Move relation execution and relation result/source selection helpers.
  3. Create `MainForm.Display.cs` for tab switching, zoom/pan/reset synchronization, and maximized viewer behavior.
  4. Create `MainForm.Cache.cs` for ROI, preprocessing, mask, overlay, and generation cache ownership.
  5. Remove unused methods/usings only after reference search and a clean build.

Do not change behavior while splitting. After each move, build the solution and verify image loading, saved ROI, preprocessing, relation source selection, processed overlays, and zoom/pan ViewState.

## Completed Features
- `讀取圖片`
  - Left function list item opens an `OpenFileDialog`.
  - Supported image filters: BMP, JPG/JPEG, PNG, TIF/TIFF, all files.
  - Images are checked when loaded. If already grayscale, they are used directly.
  - If a user accidentally selects a color image, it is converted to grayscale in memory before display; no temporary image file is saved and reloaded.
  - Large-image source tiles are decoded as `Gray8`. The WIC source format is checked; non-grayscale inputs are explicitly converted to `Gray8` before display or analysis.
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
    - Preview surfaces are capped at `2048 x 2048`. Do not restore the retired `4096 x 4096` preview level: it adds no detail once source tiles are available and can cause GDI+ `ArgumentException` under stress.
    - Loading an image, including startup restoration of `LastImagePath`, restores only the original image and ROI. It must not automatically build a processed ROI mask. The first processing run begins only when the user selects a processing step or group.
  - Large-image processed previews use the same tiled display path:
    - Processed viewers load the original large image through `LargeImageSource`.
    - The viewer paints original image preview/tiles first.
    - `MainForm` handles `LargeImageOverlayPaint` to draw cached overlay tiles and queue missing ROI-intersecting tiles for background calculation.
    - Do not compute edge masks synchronously inside Paint; doing so can freeze the UI when switching to the `處理後` tab.
    - Large processed overlay tiles are `128 x 128` and queue at most 2 background tile calculations at once so large ROIs do not flood the thread pool.
    - `LargeImageSource.CreateRegionBitmap` must not hold the shared source lock while WIC decodes a region; otherwise overlay calculation can block original image tile refresh and appear stuck.
    - Processed overlay calculation should prefer `LargeImageSource.TryCreateRegionBitmapFromCachedTile`. It crops from already-cached `1024 x 1024` source tiles instead of asking WIC to decode many tiny overlay regions. If the source tile is missing, queue the source tile and retry on repaint.
    - If the high-resolution source tile is not cached yet, processed overlay falls back to `LargeImageSource.GetBestPreview(0f)` so the `處理後` tab does not stay blank with an endless pending count. When the high-resolution tile finishes loading through `QueueTile`, the preview overlay cache is invalidated and recalculated.
    - Current preferred large-image processed preview flow: build one ROI-level mask for each selected processing step, then render red overlay from that prepared result. The display path must not run edge algorithms per visible tile; pan/zoom redraws only from the prepared mask.
    - Canny stores its full-resolution result as an OpenCV `Cv.Mat` binary mask. The processed viewer creates and caches one red overlay for the currently visible source rectangle directly from that mask. Do not reintroduce a background display-tile queue for Canny: it previously produced incomplete red line segments, status-bar flicker, and redraws that only completed after panning.
    - The viewport overlay is display-only. It may use `Area` reduction below 1x and `Nearest` enlargement at detail zoom; contour/measurement work must continue to use the full-resolution `Cv.Mat` mask.
    - When reusing the ROI grayscale `Cv.Mat` for a second Canny step, return a sub-matrix header sharing the native buffer, not `Clone()`. A full clone of a 16K-scale ROI duplicates hundreds of MB and makes later steps much slower than the first.
    - Selecting an already-completed step must explicitly schedule a processed-view refresh. `StartLargeProcessedMaskBuild` returns immediately for a cache hit and otherwise no completion callback exists to trigger redraw.
    - The lower status bar records three independent times after Canny completes: `ROI 處理時間` (ROI tile/gray Mat preparation), `影像處理時間` (Gaussian Blur + Canny only), and `顯示時間` (visible red overlay creation only). Do not combine these values; cached ROI preparation should show a much smaller ROI time on later runs.
    - The current configuration processes the entire ROI in one OpenCV pass (`MaxSinglePassLargeRoiPixels = long.MaxValue`) to keep one continuous algorithm result. This is correct for the result but can require several GB of native and managed memory for a huge ROI; do not assume it will be instantaneous or safe for arbitrary ROI sizes.
    - A chunked `1024 x 1024` implementation with padding remains in the code as a fallback path if the single-pass limit is lowered later. Padding must be retained if it is re-enabled to avoid visual seams.
    - Clearing the red overlay tile bitmap cache must not clear the completed ROI-level mask; otherwise the processed view can finish, invalidate, then start building the same mask again.
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
  - Left-click `指定 ROI` expands/collapses `ROI 1`, `ROI 2`, `ROI 3`, ... .
  - Right-click `指定 ROI` shows `新增 ROI` and `顯示全部`.
  - Right-click an existing `ROI N` shows `刪除`.
  - `新增 ROI` enables ROI drawing on currently visible `原圖` viewers that already have an image.
  - User drags a green rectangle on left or right `原圖`.
  - When mouse is released, a confirmation dialog asks whether to keep the ROI.
  - Confirmed ROI is appended to the ROI list and stored in image coordinates.
  - Selecting `ROI N` makes it the active ROI and shows its green overlay. Processing previews apply the selected step to every saved ROI; the processed image remains full-frame.
  - `刪除` asks for confirmation before removing that ROI.
  - ROI list state is persisted in `SystemParameters.ini` under `[ROIs]`; legacy single `[ROI]` values are still loaded and converted into `ROI 1`.

- `影像處理`
  - Left function item expands/collapses child items at runtime.
  - Right-click `影像處理` shows `新增影像處理`; there are no processing steps until the user adds one.
  - Each add appends a new step shown as `處理N(未決定)` when no algorithm has been selected yet.
  - Right-click a processing step shows `上移`, `下移`, `命名`, `刪除`.
  - Ctrl-select two or more processing steps and/or groups, then right-click to create a named group.
  - Groups are recursive: a group may contain steps and child groups. Left-click a group expands/collapses it; selecting a group renders the combined independent results of every descendant step. Each step still processes the original ROI, not the result of a preceding step.
  - Right-click a group shows `上移`, `下移`, `命名`, `解除群組`, `刪除`. `解除群組` preserves and lifts its contents one level; `刪除` deletes its full descendant tree after confirmation.
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
    - `Polarity Edge`: `Polarity`, `ContrastThreshold`, `CoreWidth`, `Smoothing`, `GaussianSigma`, `SearchDirection`, `BorderType`.
    - `Canny Edge`: `LowThreshold`, `HighThreshold`, `KernelSize`, `L2Gradient`, `GaussianBlurSize`, `GaussianSigma`.
      - Canny `KernelSize` is restricted to OpenCV-supported aperture values `3`, `5`, `7`. Old INI values of `9` or higher must be normalized to `7`; larger Sobel apertures belong only to Sobel Edge.
    - `Sobel Edge`: `Direction`, `KernelSize`, `Threshold`.
  - Canny kernel options are `3`, `5`, `7`; Sobel kernel options are `1`, `3`, `5`, `7`.
  - Selecting one of those methods switches the right side from the flow tree to its parameter editor. The `重新選擇方法` button returns to the flow tree.
  - Parameter edits are saved immediately into each step's `StepN.Parameters` INI field.
  - Numeric parameters use `NumericUpDown` controls. Mouse wheel adjusts values directly; holding Shift increases the step size by 10x.
  - Processing previews run only inside saved ROIs, but the `處理後` image remains full-frame. Pixels outside ROIs stay as the original grayscale image; detected `true` pixels inside ROIs are drawn in red.
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
  - Canny uses the native OpenCvSharp path for large-image ROI processing: `GaussianBlur -> Canny -> Cv.Mat binary mask`.
  - Canny must remain a raw edge detector. `Strongest`, `Longest`, `MinEdgeLength`, and `MaxGap` were removed from the Canny UI because they are contour/feature-filter concepts, not native Canny parameters. Old INI values may remain but must be ignored by Canny and must not change its mask cache key.
  - Canny, Sobel Edge, and Polarity Edge use the same native large-image OpenCV `Cv.Mat` mask pipeline. Their large-image results are cached independently from ROI preparation and rendered as viewport-local red overlays.
  - Small-image ROI extraction reads Gray8 data directly where possible. Color inputs are safely converted to grayscale at the boundary.
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
GroupCount=0
Step1.DisplayName=
Step1.GroupId=
Step1.Method=
Step1.Parameters=
Group1.Id=
Group1.ParentGroupId=
Group1.DisplayName=
```

- On startup:
  - If `LastImagePath` exists, the app reloads the previous image into both `原圖` viewers.
  - If ROI is enabled and valid, the right `原圖` restores the ROI overlay.
  - Saved ROIs are prepared in the background. Processing-result masks are not calculated until the user selects a processing step or group.

## Important Files
- `IntegratedImageProcessingApp\Forms\MainForm.cs`
  - Main menu behavior.
  - Image load flow.
  - Visible viewer sync.
  - ROI workflow.
  - Image processing tree, recursive groups, context menus, OpenCV preview dispatch, and right-side flow tree selection.
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
  - WIC-backed `Gray8` preview and tile source for huge images; safely converts color sources to grayscale.

- `IntegratedImageProcessingApp\IntegratedImageProcessingApp.csproj` and `packages.config`
  - OpenCvSharp plus explicit .NET Framework runtime dependencies: `System.Runtime.CompilerServices.Unsafe 6.0.0`, `System.Memory`, `System.Buffers`, and `System.Numerics.Vectors`. Do not remove these references: OpenCvSharp needs them at runtime.

- `IntegratedImageProcessingApp\Services\SystemParameterIniService.cs`
  - Simple INI load/save service.

- `IntegratedImageProcessingApp\Services\SystemParameterSettings.cs`
  - Settings model for image path and ROI.
  - Also stores image processing workflow steps, including each step's future method and parameters.

## Latest Commits
- `9d8040c Optimize OpenCV edge postprocessing`
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
- Current file split status:
  - `MainForm.ImageProcessing.cs` now contains the image-processing execution entry points and the actual OpenCV Canny, Sobel, and Polarity mask implementations.
  - `MainForm.cs` no longer contains `LegacyCreateOpenCvCannyMask`, `LegacyCreateOpenCvSobelMask`, or `LegacyCreateOpenCvPolarityMask`.
  - The solution has been rebuilt successfully after the split.
- Recommended next split order: `MainForm.Preprocessing.cs`, `MainForm.Relations.cs`, `MainForm.Parameters.cs`, `MainForm.Roi.cs`, `MainForm.Preview.cs`, and then `MainForm.Cache.cs`. Use `partial class MainForm` first so behavior does not change during organization.
- Do not move shared OpenCV helpers without checking all callers. Current processing code shares grayscale Mat creation, mask conversion, kernel normalization, parameter parsing, and border handling helpers.
- Direct image-processing execution and relation execution have different source rules. Direct processing uses the currently selected explicit source; relation processing uses `SourceType`/`SourceId`. Do not let `latestPreprocessedImage` alone decide the source.
- Selecting `影像前處理 > 原始影像` must explicitly set the source state to `Original`. A null source state can be ambiguous and previously caused direct processing to wait for preprocessing or use a stale preprocessing cache.
- Only a relation whose source type is `Step` or `Group` should trigger preprocessing preparation. An original-image source must not wait for preprocessing completion.
- When a relation uses preprocessing output, the OpenCV detector and the `處理後` display must use the same `LargeImageSource`; otherwise the red result can be drawn over the darker original instead of the lighter preprocessing image.
- The relation parameter panel must remain separate from the normal parameter panels. Never call `parameterPanel.Controls.Clear()` when switching relation settings, because it removes the preprocessing and image-processing panels and breaks navigation.
- Large-image processing uses full-resolution OpenCV masks and display tiles. Never clone the entire large image just to draw a viewport overlay, and never run Canny/Sobel/Polarity once per display tile.
- Common historical failures: GDI+ `ArgumentException`/`ObjectDisposedException` from drawing or cloning a tile concurrently; `OutOfMemoryException` from large Bitmap/Mat allocations; array-bound errors around tile edges; endless `ROI MASK` waits caused by duplicate scheduling or waiting for preprocessing that was not requested; stale results after changing images; and UI freezes caused by too many `BeginInvoke` status updates.
- When changing an image, invalidate all processing/preprocessing/ROI caches and generations. When changing a parameter, preserve both viewers' zoom and pan state and invalidate only the affected result.
- Keep status updates throttled; do not update the status label for every tile or ROI. Background workers must never access disposed UI controls or Bitmaps.
- Keep `MainForm.Designer.cs` changes separate. It currently contains unrelated Visual Studio designer modifications and should not be included in focused commits unless explicitly requested.
- Processed-view state is now captured from the currently visible viewer before invalidation and restored after the processed source or overlay is replaced. Preserve Zoom, Offset, and shared/maximized state; do not capture only the processed control because it may be empty on first execution.
- Large-image loading warms the shared full-resolution grayscale OpenCV cache in the background. Keep source-generation checks and reference counting intact when replacing images.
- Asynchronous overview preview creation for memory-backed results was tested and reverted because the visual result did not improve. Keep the current synchronous fast-preview behavior until a verified replacement is available.
- Preprocessing display time can exceed OpenCV algorithm time because it creates a new memory-backed display source. Processed output is faster because it reuses original tiles and adds cached mask overlays.
- Planned file split, in recommended order:
  1. `Forms\MainForm.Preprocessing.cs`: Normalize, Gaussian Blur, CLAHE, Median Blur, Sharpen, Bilateral Filter, and preprocessing-group execution.
  2. `Forms\MainForm.Relations.cs`: relation creation, source/target selectors, relation processing, rename, reorder, delete, and invalid-reference checks.
  3. `Forms\MainForm.Parameters.cs`: parameter panels, pending values, Apply/Cancel, and parameter status timing.
  4. `Forms\MainForm.Roi.cs`: ROI creation, deletion, display-all, ROI coordinate persistence, and ROI mask preparation.
  5. `Forms\MainForm.Preview.cs`: original/preprocessed/processed tabs, red mask overlay, visible-tile refresh, and view-state restoration.
  6. `Forms\MainForm.Cache.cs`: preprocessing, ROI, mask, overlay, generation, and invalidation caches.
  7. `Services\OpenCvProcessingService.cs`: move pure OpenCV operations out of the form after the partial-class split is stable.
- Use `partial class MainForm` for the first organization passes. Build after each file move and do not change behavior while relocating code. Only then extract pure services.
- Image relation UI and execution foundation is now implemented.
- The left menu contains `影像關聯`; right-clicking it can add a relation. Relation items support right-click processing, moving up/down, renaming, and deleting.
- Selecting a relation shows source and target selectors on the right. Sources include original image, preprocessing step, and preprocessing group. Targets include image-processing step and image-processing group.
- Relation settings are persisted in `[ImageRelations]` using stable GUIDs: `SourceType`, `SourceId`, `ProcessingType`, and `ProcessingId`. Display names are presentation only.
- Relation processing can be started with right-click `處理`, or by holding `A`/`a` while left-clicking the relation. A normal left-click only opens the settings panel.
- When a relation uses a preprocessing source, the OpenCV processing source and the large-image `處理後` display source are intended to share the same preprocessing `LargeImageSource`, so red results are drawn over the lighter preprocessing image rather than the darker original image.
- Preprocessing-step relation sources support stopping the preprocessing pipeline at the selected step. Preprocessing-group sources run the complete preprocessing chain.
- The relation parameter panel is separate from the normal preprocessing/image-processing parameter panels. Do not use `parameterPanel.Controls.Clear()` when changing relation selection; it removes the other panels and breaks navigation.
- Relation combo boxes show friendly names such as `修改後名字 (Normalize)` while retaining GUIDs internally.
- Direct image-processing execution clears the active relation source so a previous relation cannot affect later direct processing.
- Remaining relation work: verify full-resolution large-image behavior with multiple preprocessing steps/groups, add per-source result caches, implement exact group merge semantics, show relation processing/display timings, and add invalid-source/invalid-target warnings when referenced items are deleted.
- Image relation persistence foundation is now present. `ImageProcessingStepSettings` has a stable `Id`, and `ImageRelationSettings` stores `SourceType`, `SourceId`, `ProcessingType`, and `ProcessingId`.
- `SystemParameterIniService` reads and writes these values under `[ImageRelations]`. Legacy step entries without an ID receive a GUID during load; do not use display names or list indexes as relation keys.
- The relation editor and execution UI are not yet implemented. The next UI work should expose `影像關聯` below image processing, with source choices for original/preprocessing step/group and target choices for processing step/group.
- Be careful with `MainForm.Designer.cs`; avoid helper method calls or custom-control declarations in the main designer file if Visual Studio Designer starts failing.
- Prefer adding runtime behavior in `MainForm.cs` and reusable viewer behavior in `ImageDisplayControl.cs`.
- Treat images inside the app as grayscale-only. Color input files should be converted in memory at the boundary before display or analysis; already-grayscale input should not be converted again.
- Canny's full-resolution native `Cv.Mat` mask is the authoritative result. Its display overlay is not a replacement for the data used by later contour work.
- Do not add selection, longest-edge, or minimum-length controls back into Canny. Implement those as explicit `Find Contours` / `Feature Filter` operations later.
- Keep every native edge-result mask separate from ROI preparation data and render only from completed cached results.
- Verify a two-step Canny workflow after any cache changes: selecting a previously calculated `處理1` or `處理2` must reuse memory; identical effective Canny parameters must share one result; selecting a different effective parameter set may build one new ROI mask but must finish and not loop indefinitely.
- For a huge full-frame ROI (for example `16384 x 50000`), a one-pass OpenCV Canny/Sobel/Polarity operation needs multiple large temporary Mats. The current full-ROI setting prioritizes continuous results over memory use; test target image sizes before relying on it in production.
- There may be Visual Studio formatting-only changes in `MainForm.Designer.cs` or `MainForm.resx` after opening the designer. Inspect before committing.
