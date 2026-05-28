# Domain: Inspection

**Directory:** `Inspection`
**Files:** 5
**Symbols:** 34

## Files

### `Inspection/AutomationInspector.cs`

**Functions:**
- `InspectAtPoint` (line 42)
- `WpfPoint` (line 67)
- `ExtractElementInfo` (line 74)
- `ExtractElementInfo` (line 108)
- `GetStringProperty` (line 184)
- `GetBoolProperty` (line 197)
- `TryGetNameFromChildren` (line 215)
- `GetProcessName` (line 258)


### `Inspection/ElementInfo.cs`

**Functions:**
- `ElementInfo` (line 14)


### `Inspection/ProcessDetector.cs`

**Functions:**
- `Detect` (line 65)
- `ClearCache` (line 96)
- `ClassifyProcess` (line 108)


### `Inspection/ScreenshotCapture.cs`

**Functions:**
- `CaptureElement` (line 61)
- `DrawingRectangle` (line 132)
- `CleanupOldScreenshots` (line 159)
- `BuildFilename` (line 199)
- `EnsureFolder` (line 219)
- `ParseColor` (line 233)
- `SystemInformation_VirtualScreenLeft` (line 255)
- `SystemInformation_VirtualScreenTop` (line 256)
- `SystemInformation_VirtualScreenWidth` (line 257)
- `SystemInformation_VirtualScreenHeight` (line 258)


### `Inspection/SelectorBuilder.cs`

**Functions:**
- `BuildSelector` (line 34)
- `BuildFallbackSegment` (line 66)
- `GetParentInfo` (line 78)
- `GetSiblings` (line 101)
- `BuildSegment` (line 148)
- `BuildFallbackSegment` (line 170)
- `GetControlTypeName` (line 188)
- `GetChildIndex` (line 205)
- `RuntimeIdsEqual` (line 246)
- `GetStringProp` (line 254)
- `EscapeValue` (line 270)
- `TruncateName` (line 276)


## Change Recipe

To add a new feature to the **Inspection** domain:

1. Add or update tests
