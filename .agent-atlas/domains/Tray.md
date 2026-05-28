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
- `OnCopySingleClicked` (line 551)
- `OnEditQueryClicked` (line 559)
- `OnViewShotClicked` (line 585)
- `OnRemoveClicked` (line 613)
- `OnClearAllClicked` (line 622)
- `OnCopyAllClicked` (line 627)
- `OnHotkeyPressed` (line 651)
- `OnSessionChanged` (line 674)
- `FindElement` (line 685)
- `BuildElementDescription` (line 696)
- `Truncate` (line 710)
- `RunScreenshotCleanup` (line 713)
- `RefreshTooltip` (line 731)
- `DisposeCurrentMenu` (line 746)
- `OnTrayIconMouseClick` (line 760)


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
