# Domain: Picker

**Directory:** `Picker`
**Files:** 5
**Symbols:** 55

## Files

### `Picker/ElementHighlighter.cs`

**Classes:**
- `ElementHighlighter` (line 20)

**Functions:**
- `ElementHighlighter` (line 39)
- `Highlight` (line 57)
- `Clear` (line 104)
- `CreateRectangle` (line 117)
- `ApplyColors` (line 134)
- `ParseColor` (line 151)


### `Picker/ElementPicker.cs`

**Classes:**
- `ElementPicker` (line 34)

**Functions:**
- `ElementPicker` (line 84)
- `Dispose` (line 86)
- `PickAsync` (line 105)
- `EnsureWpfDispatcher` (line 119)
- `EnsureWpfDispatcher` (line 164)
- `OnPollTimerTick` (line 199)
- `InstallHooks` (line 246)
- `MouseHookCallback` (line 280)
- `if` (line 292)
- `KeyboardHookCallback` (line 306)
- `CleanupSession` (line 329)


### `Picker/OverlayWindow.xaml.cs`

**Functions:**
- `OverlayWindow` (line 24)
- `ShowOverlay` (line 45)
- `HideOverlay` (line 57)
- `SetFrozenBackground` (line 67)
- `SetInteractive` (line 98)
- `OnSourceInitialized` (line 121)
- `OnLoaded` (line 143)
- `ApplyVirtualScreenBounds` (line 160)


### `Picker/QueryDialog.cs`

**Classes:**
- `QueryDialog` (line 18)

**Functions:**
- `QueryDialog` (line 31)
- `Size` (line 145)
- `AutoGrowTextBox` (line 175)
- `PositionNearCursor` (line 211)
- `OnTextBoxKeyDown` (line 235)
- `ApplyPlaceholder` (line 253)
- `BuildHeaderText` (line 294)
- `LoadAppIcon` (line 304)


### `Picker/SpotPicker.cs`

**Classes:**
- `SpotResult` (line 24)
- `SpotPicker` (line 52)

**Functions:**
- `SpotPicker` (line 91)
- `Dispose` (line 93)
- `PickAsync` (line 103)
- `EnsureWpfDispatcher` (line 119)
- `EnsureWpfDispatcher` (line 166)
- `InstallHooks` (line 192)
- `MouseHookCallback` (line 222)
- `if` (line 237)
- `if` (line 244)
- `if` (line 291)
- `KeyboardHookCallback` (line 303)
- `UpdateSelectionRect` (line 326)
- `HideSelectionRect` (line 373)
- `CleanupSession` (line 387)
- `SetCrosshairCursor` (line 437)
- `RestoreSystemCursors` (line 454)
- `CreateSelectionRect` (line 471)


## Change Recipe

To add a new feature to the **Picker** domain:

1. Add the new file under `Picker/`
2. Export from the domain index if applicable
