# Domain: Tray

**Directory:** `Tray`
**Files:** 3
**Symbols:** 33

## Files

### `Tray/IconGenerator.cs`

**Functions:**
- `CreateIdleIcon` (line 30)
- `CreateActiveIcon` (line 42)
- `CreateIcon` (line 54)
- `Rectangle` (line 72)
- `RoundedRect` (line 144)


### `Tray/TrayApplication.cs`

**Classes:**
- `TrayApplication` (line 27)

**Functions:**
- `TrayApplication` (line 63)
- `UpdateElementCount` (line 117)
- `BuildMenu` (line 138)
- `OnSettingsClicked` (line 160)
- `CopySettings` (line 186)
- `OnPickElementClicked` (line 213)
- `OnPickSpotClicked` (line 395)
- `OnCopySingleClicked` (line 561)
- `OnEditQueryClicked` (line 569)
- `OnViewShotClicked` (line 595)
- `OnRemoveClicked` (line 623)
- `OnClearAllClicked` (line 632)
- `OnCopyAllClicked` (line 637)
- `OnHotkeyPressed` (line 661)
- `OnSessionChanged` (line 684)
- `FindElement` (line 695)
- `BuildElementDescription` (line 706)
- `Truncate` (line 720)
- `RunScreenshotCleanup` (line 723)
- `RefreshTooltip` (line 741)
- `DisposeCurrentMenu` (line 756)
- `OnTrayIconMouseClick` (line 770)


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
