# Domain: Settings

**Directory:** `Settings`
**Files:** 3
**Symbols:** 22

## Files

### `Settings/AppSettings.cs`

**Classes:**
- `AppSettings` (line 9)


### `Settings/SettingsDialog.cs`

**Classes:**
- `SettingsDialog` (line 14)

**Functions:**
- `SettingsDialog` (line 35)
- `MakeFieldLabel` (line 333)
- `MakeHLine` (line 340)
- `OnHotkeyBoxKeyDown` (line 352)
- `OnBrowseClicked` (line 375)
- `OnAutoCopyChanged` (line 387)
- `OnAutoCleanChanged` (line 392)
- `OnColorButtonClicked` (line 400)
- `OnOpacityChanged` (line 416)
- `OnSaveClicked` (line 425)
- `ShowError` (line 489)
- `FormatOpacity` (line 492)
- `TryParseHexColor` (line 494)
- `ParseColorSafe` (line 513)
- `GetContrastColor` (line 516)
- `LoadAppIcon` (line 522)


### `Settings/SettingsManager.cs`

**Functions:**
- `Load` (line 46)
- `Save` (line 87)
- `SetAutoStart` (line 122)
- `GetAutoStartEnabled` (line 159)


## Change Recipe

To add a new feature to the **Settings** domain:

1. Add the new file under `Settings/`
2. Export from the domain index if applicable
