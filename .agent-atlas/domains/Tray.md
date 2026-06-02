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
- `TrayApplication` (line 27)

**Functions:**
- `TrayApplication` (line 64)
- `UpdateElementCount` (line 116)
- `BuildMenu` (line 152)
- `OnSettingsClicked` (line 174)
- `CopySettings` (line 200)
- `OnPickElementClicked` (line 227)
- `OnPickSpotClicked` (line 409)
- `OnCopySingleClicked` (line 575)
- `OnEditQueryClicked` (line 583)
- `OnViewShotClicked` (line 609)
- `OnRemoveClicked` (line 637)
- `OnClearAllClicked` (line 646)
- `OnCopyAllClicked` (line 651)
- `OnHotkeyPressed` (line 675)
- `OnSessionChanged` (line 698)
- `FindElement` (line 709)
- `BuildElementDescription` (line 720)
- `Truncate` (line 734)
- `RunScreenshotCleanup` (line 737)
- `RefreshTooltip` (line 755)
- `DisposeCurrentMenu` (line 770)
- `OnTrayIconMouseClick` (line 784)


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
