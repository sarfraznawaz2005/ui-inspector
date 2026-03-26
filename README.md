# UI Inspector

A Windows system tray application that inspects UI elements for AI coding agents. It uses UI Automation to detect elements at screen coordinates, capture screenshots, build CSS-like selectors, and export structured markdown to the clipboard.

## Features

- **Interactive element picking** — transparent overlay with real-time highlighting across all monitors
- **Spot (region) picking** — draw a rectangle over any screen area to capture a region screenshot and detect the element at its center
- **Cross-framework support** — works with WinForms, WPF, Chromium browsers, Electron, and WebView2 apps
- **CSS-like selectors** — e.g. `Window[@Name='Notepad'] > Document > Edit[@Id='Text Area']`
- **Context capture** — parent element info and up to 5 sibling descriptions per element
- **Automatic screenshots** — element region with padding and highlight border, saved as PNG
- **Markdown clipboard export** — structured element details ready to paste into an AI chat
- **Session management** — accumulate multiple captures, edit annotations, remove individual elements
- **Global hotkeys** — system-wide shortcuts that work while any application is focused
- **Auto-cleanup** — screenshots older than 24 hours are deleted on startup
- **Single-instance enforcement** — prevents duplicate instances via a named mutex

## Requirements

- Windows 10 or Windows 11 (x64)
- .NET 8.0 runtime (bundled in self-contained builds)

## Installation

Download or build `UIInspector.exe` and run it — no installer needed. The app appears in the system tray.

## Usage

| Action | Default Hotkey |
|--------|----------------|
| Pick an element | `Ctrl+Shift+I` |
| Pick a spot (region) | `Ctrl+Shift+S` |
| Copy all to clipboard | `Ctrl+Shift+C` |

### Picking an element

1. Press `Ctrl+Shift+I` or right-click the tray icon and select **Pick Element**
2. Move the cursor over any UI element — a highlight rectangle follows it
3. **Left-click** to capture the element, or **right-click / Escape** to cancel
4. Optionally enter a note in the query dialog (closing or clicking Skip discards the capture)
5. The element's details are added to the session and auto-copied to the clipboard

### Picking a spot (region)

1. Press `Ctrl+Shift+S` or right-click the tray icon and select **Pick Spot**
2. The cursor changes to a system-wide crosshair
3. **Click and drag** to draw a rectangle over any area of the screen
4. On release, a screenshot of the drawn region is captured and the UI element at its center is detected (if any)
5. Optionally enter a note in the query dialog
6. The result is added to the session — if no UI Automation element was found, the control type is recorded as "Region"

### Clipboard output

```markdown
# UI Details (1 element)
**App**: notepad | **Captured**: 2026-03-01 14:30:00

## Element 1: Edit "Text Area"
- **Type**: Edit
- **Name**: "Text Area"
- **AutomationId**: 15
- **ClassName**: RichEditD2DPT
- **Selector**: `Window[@Name='Untitled - Notepad'] > Document > Edit[@Id='15']`
- **State**: Enabled=true, Visible=true
- **Bounds**: x:12 y:85 w:800 h:520
- **Process**: notepad (NativeDesktop)
- **Screenshot**: `C:\Users\...\AppData\Local\Temp\ui-inspector\elem-20260301-143000-001.png`
- **Parent**: Document "Text Editor"
- **Siblings**: 2 (ScrollBar "Vertical", ScrollBar "Horizontal")

---

User Query: See screenshot `C:\Users\...\AppData\Local\Temp\ui-inspector\elem-20260301-143000-001.png`
```

When multiple elements are captured, the header shows the total count and lists all unique process names. Each element block is separated by `---`. If a user query was entered, it is appended after the screenshot path.

## Tray Menu

Left-clicking or right-clicking the tray icon opens the context menu:

```
Pick Element        Ctrl+Shift+I
Pick Spot           Ctrl+Shift+S
─────────────────────────────────
[1] Button "Submit Order"
    > Copy / Edit Query / View Screenshot / Remove
[2] Edit "Username"
    > Copy / Edit Query / View Screenshot / Remove
─────────────────────────────────
Copy All (2)        Ctrl+Shift+C
Clear All
─────────────────────────────────
Settings
Exit
```

Each captured element has a submenu with options to copy its markdown, edit the query annotation, view the screenshot, or remove it from the session.

## Settings

Right-click the tray icon and select **Settings**, or edit `%APPDATA%\UIInspector\settings.json` directly.

| Setting | Default | Description |
|---------|---------|-------------|
| PickHotkey | `Ctrl+Shift+I` | Hotkey to start element picking |
| SpotHotkey | `Ctrl+Shift+S` | Hotkey to start spot (region) picking |
| CopyHotkey | `Ctrl+Shift+C` | Hotkey to copy session to clipboard |
| AutoCopy | `true` | Auto-copy each element on capture |
| AutoClearBeforeCopy | `false` | Clear session after auto-copy (screenshots kept on disk) |
| ScreenshotFolder | `%TEMP%\ui-inspector` | Where screenshots are saved |
| AutoCleanScreenshots | `true` | Delete old screenshots on startup |
| CleanAfterHours | `24` | Age threshold for screenshot cleanup (1–168 hours) |
| StartWithWindows | `false` | Launch on login (writes to `HKCU\...\Run`) |
| HighlightColor | `#4488FF` | Highlight rectangle color |
| HighlightOpacity | `0.3` | Highlight fill opacity (0.1–0.8) |

## Building from Source

```bash
# Debug build & run
dotnet build
dotnet run

# Release — self-contained single EXE
./build.ps1 -Mode self-contained

# Release — requires .NET runtime on target machine
./build.ps1 -Mode framework-dependent
```

Output: `dist/UIInspector.exe`

## Data Locations

| Path | Contents |
|------|----------|
| `%APPDATA%\UIInspector\settings.json` | User settings |
| `%APPDATA%\UIInspector\error.log` | Error log (truncated at 1 MB) |
| `%TEMP%\ui-inspector\` | Screenshots (default, configurable) |
