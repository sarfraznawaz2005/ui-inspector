# Domain: Picker

**Directory:** `Picker`
**Files:** 5
**Symbols:** 54

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
- `SetInteractive` (line 70)
- `OnSourceInitialized` (line 93)
- `OnLoaded` (line 115)
- `ApplyVirtualScreenBounds` (line 132)


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
- `SpotPicker` (line 45)

**Functions:**
- `SpotPicker` (line 84)
- `Dispose` (line 86)
- `PickAsync` (line 96)
- `EnsureWpfDispatcher` (line 108)
- `EnsureWpfDispatcher` (line 136)
- `InstallHooks` (line 162)
- `MouseHookCallback` (line 192)
- `if` (line 207)
- `if` (line 214)
- `if` (line 261)
- `KeyboardHookCallback` (line 273)
- `UpdateSelectionRect` (line 296)
- `HideSelectionRect` (line 343)
- `CleanupSession` (line 357)
- `SetCrosshairCursor` (line 407)
- `RestoreSystemCursors` (line 424)
- `CreateSelectionRect` (line 441)


## Change Recipe

To add a new feature to the **Picker** domain:

1. Add the new file under `Picker/`
2. Export from the domain index if applicable
