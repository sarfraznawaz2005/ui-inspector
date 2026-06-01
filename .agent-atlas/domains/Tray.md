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
- `UpdateElementCount` (line 119)
- `BuildMenu` (line 140)
- `OnSettingsClicked` (line 162)
- `CopySettings` (line 188)
- `OnPickElementClicked` (line 215)
- `OnPickSpotClicked` (line 397)
- `OnCopySingleClicked` (line 563)
- `OnEditQueryClicked` (line 571)
- `OnViewShotClicked` (line 597)
- `OnRemoveClicked` (line 625)
- `OnClearAllClicked` (line 634)
- `OnCopyAllClicked` (line 639)
- `OnHotkeyPressed` (line 663)
- `OnSessionChanged` (line 686)
- `FindElement` (line 697)
- `BuildElementDescription` (line 708)
- `Truncate` (line 722)
- `RunScreenshotCleanup` (line 725)
- `RefreshTooltip` (line 743)
- `DisposeCurrentMenu` (line 758)
- `OnTrayIconMouseClick` (line 772)


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
