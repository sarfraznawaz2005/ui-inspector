# Domain: Tray

**Directory:** `Tray`
**Files:** 3
**Symbols:** 34

## Files

### `Tray/IconGenerator.cs`

**Functions:**
- `CreateIdleIcon` (line 31)
- `CreateActiveIcon` (line 43)
- `CreateBadgedIcon` (line 61)
- `CreateIcon` (line 140)
- `Rectangle` (line 158)
- `RoundedRect` (line 230)


### `Tray/TrayApplication.cs`

**Classes:**
- `TrayApplication` (line 28)

**Functions:**
- `TrayApplication` (line 65)
- `UpdateElementCount` (line 117)
- `BuildMenu` (line 153)
- `OnSettingsClicked` (line 175)
- `CopySettings` (line 201)
- `OnPickElementClicked` (line 228)
- `OnPickSpotClicked` (line 397)
- `OnCopySingleClicked` (line 576)
- `OnEditQueryClicked` (line 584)
- `OnViewShotClicked` (line 610)
- `OnRemoveClicked` (line 638)
- `OnClearAllClicked` (line 647)
- `OnCopyAllClicked` (line 652)
- `OnHotkeyPressed` (line 676)
- `OnSessionChanged` (line 699)
- `FindElement` (line 710)
- `BuildElementDescription` (line 721)
- `Truncate` (line 735)
- `RunScreenshotCleanup` (line 738)
- `RefreshTooltip` (line 756)
- `DisposeCurrentMenu` (line 771)
- `OnTrayIconMouseClick` (line 785)


### `Tray/TrayMenuBuilder.cs`

**Functions:**
- `Build` (line 52)
- `BuildElementMenuItem` (line 198)
- `OnExitClicked` (line 268)
- `TruncateName` (line 277)
- `TruncateQuery` (line 292)


## Change Recipe

To add a new feature to the **Tray** domain:

1. Add the new file under `Tray/`
2. Export from the domain index if applicable
