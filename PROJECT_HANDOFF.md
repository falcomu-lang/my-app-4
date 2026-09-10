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
  - Selected image is loaded into both left and right `原圖` tabs.
  - Both sides automatically switch to `原圖`.

- Image viewer interaction
  - Mouse wheel zoom.
  - Left-button drag pan.
  - Fit/reset view button.
  - Status text shows zoom, offset, and image coordinate information.
  - Behavior is modeled after the viewer interaction in:
    `C:\Users\falcomu\Documents\Codex\程式撰寫 專案資料夾\攝影機影像擷取\my-app-2`

- Visible left/right view synchronization
  - Zoom/pan/reset sync only between the currently visible left and right image controls.
  - Hidden tabs are not updated to avoid unnecessary work when more images are added later.
  - When switching visible tabs, the visible right viewer can sync from the visible left viewer if both have images.

- `指定 ROI`
  - Left function item expands/collapses child items at runtime:
    - `設定 ROI`
    - `取消 ROI`
  - `設定 ROI` enables ROI drawing on currently visible `原圖` viewers that already have an image.
  - User drags a green rectangle on left or right `原圖`.
  - When mouse is released, a confirmation dialog asks whether to keep the ROI.
  - Confirmed ROI is always displayed only on the right `原圖`.
  - ROI is stored in image coordinates, so it stays fitted to the image while the right viewer zooms/pans/resets.
  - `取消 ROI` clears the right-side ROI and updates settings.

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
```

- On startup:
  - If `LastImagePath` exists, the app reloads the previous image into both `原圖` viewers.
  - If ROI is enabled and valid, the right `原圖` restores the ROI overlay.

## Important Files
- `IntegratedImageProcessingApp\Forms\MainForm.cs`
  - Main menu behavior.
  - Image load flow.
  - Visible viewer sync.
  - ROI workflow.
  - Startup restore from INI.

- `IntegratedImageProcessingApp\Forms\MainForm.Designer.cs`
  - Designer-editable main frame and placeholder controls.
  - Keep this file friendly to Visual Studio Designer.

- `IntegratedImageProcessingApp\Controls\ImageDisplayControl.cs`
  - Runtime image loading.
  - Viewer paint, zoom, pan, fit view.
  - ROI selection and ROI overlay drawing.
  - View sync event/state.

- `IntegratedImageProcessingApp\Services\SystemParameterIniService.cs`
  - Simple INI load/save service.

- `IntegratedImageProcessingApp\Services\SystemParameterSettings.cs`
  - Settings model for image path and ROI.

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
- There may be Visual Studio formatting-only changes in `MainForm.Designer.cs` or `MainForm.resx` after opening the designer. Inspect before committing.
