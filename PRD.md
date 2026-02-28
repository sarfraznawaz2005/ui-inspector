# Product Requirements Document: UI Inspector

**Version**: 1.0
**Date**: 2026-02-28
**Status**: Draft

---

## 1. Executive Summary

**UI Inspector** is a lightweight Windows system tray application that enables developers to point-and-click on any UI element in desktop or web applications, capture its automation properties, screenshot, and optional user query, then copy everything to the clipboard as structured markdown. This output can be pasted into any AI coding agent (Claude Code, Cursor, Gemini CLI, OpenCode, etc.) to provide precise UI context for debugging, modification, or development tasks.

### Problem Statement

When developers use AI agents to work on desktop applications, there is no equivalent to the browser's "Inspect Element" workflow. Developers cannot easily communicate *which specific UI element* they're referring to. They resort to vague descriptions ("the button on the right") or manual screenshots without structural context, leading to wasted back-and-forth with AI agents.

### Solution

A zero-footprint system tray tool that:
- Lives entirely in the system tray with no persistent windows
- Lets users pick UI elements with a visual overlay (like browser DevTools inspect)
- Captures automation properties, selectors, and screenshots
- Supports multi-element selection with per-element user queries
- Copies everything as structured markdown to clipboard
- Works with **any** AI agent via simple paste — no integrations needed

### Key Design Principles

1. **Zero UI footprint** — No windows, no floating toolbars. Tray icon + context menu only.
2. **Clipboard is the API** — No MCP servers, no plugins, no agent-specific integrations. Copy/paste works everywhere.
3. **Zero third-party dependencies** — Built entirely on .NET 8 built-in libraries (WPF, WinForms, System.Windows.Automation).
4. **Smart detection** — Automatically detects whether the target is a native desktop app or a browser and uses the appropriate inspection method.

---

## 2. Target Users

### Primary: Desktop Application Developers
- Building apps with WPF, WinForms, WinUI, MAUI, Qt, Avalonia
- Using AI coding agents (CLI or IDE-based) for development assistance
- Need to quickly communicate UI element context to AI agents

### Secondary: Web Developers (Supplementary Use)
- Working with web apps in browsers
- Want a unified pick-and-paste workflow instead of switching to browser DevTools
- Working with Electron/WebView2 hybrid apps

### Tertiary: QA Engineers
- Capturing UI element details for bug reports
- Documenting automation selectors for test scripts

---

## 3. User Stories

### Core Workflow

**US-01**: As a developer, I want to launch the app and have it sit invisibly in the system tray, so it doesn't consume screen space or attention.

**US-02**: As a developer, I want to left-click the tray icon to see a context menu with all available actions, so I can quickly access pick, copy, and settings functionality.

**US-03**: As a developer, I want to activate "Pick Element" mode (via menu or hotkey), hover over any UI element on screen, see it highlighted, and click to capture it, so I can precisely identify the element I want the AI to work on.

**US-04**: As a developer, I want to type an optional query/description after picking each element (e.g., "this button stays disabled after form is filled"), so the AI understands what issue I'm describing.

**US-05**: As a developer, I want to pick multiple elements in sequence, each with its own query, so I can describe multiple related issues in one paste.

**US-06**: As a developer, I want to "Copy All" captured elements to clipboard as structured markdown, so I can paste it into any AI agent prompt.

**US-07**: As a developer, I want the clipboard output to include file paths to screenshots of each captured element, so the AI can visually inspect the elements.

### Element Inspection

**US-08**: As a developer, I want the tool to capture the element's automation properties (AutomationId, Name, ControlType, ClassName, enabled state, bounding rectangle), so the AI has enough context to find and modify the element in source code.

**US-09**: As a developer, I want the tool to generate a selector path (e.g., `Window[@Id='Main'] > Pane > Button[@Id='btnSubmit']`), so the AI can uniquely identify the element in the automation tree.

**US-10**: As a developer, I want the tool to capture parent and sibling elements, so the AI understands the element's structural context.

**US-11**: As a developer, I want the tool to detect if I clicked inside a browser (Chrome, Edge, Firefox) and use the appropriate inspection method (CDP for Chromium, or UIA fallback), so I get the best possible element data regardless of app type.

### Session Management

**US-12**: As a developer, I want to see all captured elements in the tray context menu with their names and queries, so I can review what I've captured before copying.

**US-13**: As a developer, I want to edit the query for any captured element via submenu, so I can fix typos or refine my description.

**US-14**: As a developer, I want to remove individual captured elements, so I can discard accidental picks.

**US-15**: As a developer, I want to "Clear All" to start a fresh session, so I can begin a new inspection round.

### Configuration

**US-16**: As a developer, I want customizable global hotkeys (default: Ctrl+Shift+I for pick, Ctrl+Shift+C for copy), so I can activate features without touching the mouse.

**US-17**: As a developer, I want to configure the screenshot storage location (default: `%TEMP%\ui-inspector`), so I control where files are saved.

**US-18**: As a developer, I want the option to auto-start the app with Windows, so it's always available.

**US-19**: As a developer, I want automatic cleanup of old screenshots (configurable retention period), so temp storage doesn't grow unbounded.

---

## 4. Functional Requirements

### FR-01: System Tray Icon

| Attribute | Detail |
|-----------|--------|
| **Behavior** | App starts minimized to system tray. No main window. |
| **Left-click** | Opens context menu with all actions |
| **Right-click** | Same as left-click (consistent behavior) |
| **Tooltip** | Shows app name + number of captured elements (e.g., "UI Inspector - 3 elements") |
| **Icon states** | Idle (default icon), Active (different icon when elements are captured) |
| **Single instance** | Only one instance allowed via named Mutex |

### FR-02: Context Menu

| Item | Behavior |
|------|----------|
| **Pick Element** | Activates pick mode. Shows hotkey hint. |
| **--- separator ---** | |
| **[n] ControlType "Name"** | One item per captured element. Submenu with: Edit Query, View Screenshot, Remove. Shows truncated query as gray subtitle. |
| *"No elements captured"* | Shown when session is empty (disabled/gray). |
| **--- separator ---** | |
| **Copy All (n)** | Copies all elements as markdown to clipboard. Disabled when empty. Shows hotkey hint. |
| **Clear All** | Clears all captured elements and associated screenshots. |
| **--- separator ---** | |
| **Settings** | Opens settings dialog. |
| **Exit** | Cleans up and exits. |

### FR-03: Pick Mode (Element Selection)

| Attribute | Detail |
|-----------|--------|
| **Activation** | Via context menu "Pick Element" or global hotkey |
| **Overlay** | Fullscreen, transparent, always-on-top WPF window |
| **Highlight** | As user moves mouse, the element under cursor is highlighted with a colored border/overlay (e.g., 2px blue border with semi-transparent blue fill) |
| **Element detection** | Uses `AutomationElement.FromPoint()` for the element under cursor |
| **Click to capture** | Left-click captures the highlighted element |
| **Cancel** | Escape key or right-click cancels pick mode |
| **Post-capture** | Overlay closes, query dialog appears |

### FR-04: Query Dialog

| Attribute | Detail |
|-----------|--------|
| **Trigger** | Appears immediately after element capture |
| **Position** | Near the cursor position, or near the captured element |
| **Content** | Shows element type + name as header. Multi-line text input for query. |
| **Actions** | "Add" button (or Enter) to save, "Skip" button to add without query |
| **Dismissal** | After Add/Skip, dialog closes. Element added to session. |

### FR-05: Clipboard Export

The "Copy All" action produces markdown text in the clipboard with the following structure per element:

```
## Element N: ControlType "Name"
- **Type**: ControlType
- **Name**: "Element Name"
- **AutomationId**: elementId
- **Selector**: `Window[@Id='...'] > ... > ControlType[@Id='...']`
- **State**: Enabled=true/false, Visible=true/false
- **Bounds**: x:N y:N w:N h:N
- **Process**: process.exe (Framework)
- **Screenshot**: `C:\...\temp\ui-inspector\elem-001.png`
- **Query**: "user's description of the issue"
```

### FR-06: Screenshot Capture

| Attribute | Detail |
|-----------|--------|
| **Capture area** | Bounding rectangle of the captured element |
| **Format** | PNG |
| **Storage** | `%TEMP%\ui-inspector\` by default (configurable) |
| **Naming** | `elem-{timestamp}-{sequence}.png` |
| **Highlight** | Screenshot includes a colored border around the element |
| **Cleanup** | Automatic cleanup of files older than configured retention (default 24h) |

### FR-07: Process Detection

| Process Type | Detection Method | Inspection Strategy |
|-------------|-----------------|---------------------|
| **Native desktop app** | Process name not in browser list | UI Automation (UIA) |
| **Chromium browser** (Chrome, Edge, Brave) | Process name match | CDP via localhost:9222 (Phase 2) |
| **Firefox** | Process name match | UIA fallback (Phase 1), Remote Debug Protocol (Phase 2) |
| **Electron app** | Detect electron.dll in process modules | CDP if available, UIA fallback |
| **WebView2 app** | Detect WebView2Loader.dll | CDP + UIA for shell |

Phase 1 uses UIA for everything. Phase 2 adds CDP for richer browser inspection.

### FR-08: Global Hotkeys

| Hotkey | Action | Customizable |
|--------|--------|-------------|
| **Ctrl+Shift+I** | Activate pick mode | Yes |
| **Ctrl+Shift+C** | Copy all to clipboard | Yes |
| **Escape** | Cancel pick mode (only during pick) | No |

### FR-09: Settings

Persisted to `%APPDATA%\UIInspector\settings.json`:

| Setting | Type | Default |
|---------|------|---------|
| `pickHotkey` | string | "Ctrl+Shift+I" |
| `copyHotkey` | string | "Ctrl+Shift+C" |
| `screenshotFolder` | string | "%TEMP%\\ui-inspector" |
| `autoCleanScreenshots` | bool | true |
| `cleanAfterHours` | int | 24 |
| `startWithWindows` | bool | false |
| `highlightColor` | string | "#4488FF" |
| `highlightOpacity` | float | 0.3 |

---

## 5. Non-Functional Requirements

### NFR-01: Performance
- Pick mode overlay must track mouse movement at 60fps with no perceptible lag
- `AutomationElement.FromPoint()` call must complete within 50ms
- Clipboard copy must complete within 500ms for up to 20 elements
- Screenshot capture must complete within 200ms per element
- App idle memory footprint: < 30 MB

### NFR-02: Reliability
- App must not crash if the target application closes during inspection
- UIA calls must be wrapped with timeouts (2s) to handle hung/unresponsive apps
- Single-instance enforcement via named Mutex
- Graceful handling of elements with missing/null automation properties

### NFR-03: Security
- No elevated privileges required (no UAC prompt)
- Screenshots stored in user-scoped temp directory
- No network communication (fully offline)
- No telemetry or data collection

### NFR-04: Compatibility
- Windows 10 (1809+) and Windows 11
- .NET 8.0+ runtime (or self-contained deployment)
- Must work with: WPF, WinForms, WinUI 3, MAUI, Qt, Avalonia, Electron, Java Swing/JavaFX apps (via UIA)

### NFR-05: Size & Dependencies
- Zero third-party NuGet packages
- Framework-dependent binary: < 10 MB
- Self-contained binary: < 50 MB

---

## 6. Technical Architecture

### Technology Stack

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Runtime** | .NET 8.0, `net8.0-windows` | Latest LTS, built-in WPF + WinForms |
| **Tray icon + menu** | WinForms (`NotifyIcon`, `ContextMenuStrip`) | Simplest tray implementation, built-in |
| **Pick overlay** | WPF (`Window` with `AllowsTransparency`) | Supports true transparency and layered windows |
| **Query dialog** | WinForms (`Form`) | Lightweight, quick to show/hide |
| **UI Automation** | `System.Windows.Automation` (built-in) | No third-party needed |
| **Screenshots** | `System.Drawing` (GDI+) | Built-in, reliable |
| **JSON serialization** | `System.Text.Json` | Built-in, fast |
| **Global hotkeys** | Win32 `RegisterHotKey` via P/Invoke | Built-in Windows API |

### Project Structure

```
UIInspector/
├── UIInspector.csproj
├── Program.cs                         # Entry point, single-instance, app lifecycle
├── Tray/
│   ├── TrayApplication.cs            # NotifyIcon setup, lifecycle, event routing
│   ├── TrayMenuBuilder.cs            # Dynamic context menu construction
│   └── Icons/
│       ├── icon-idle.ico             # Default tray icon
│       └── icon-active.ico           # Icon when elements are captured
├── Picker/
│   ├── OverlayWindow.xaml/.cs        # Transparent fullscreen WPF overlay
│   ├── ElementHighlighter.cs         # Draws highlight rectangle on overlay
│   ├── ElementPicker.cs              # Mouse tracking + UIA.FromPoint coordination
│   └── QueryDialog.cs               # Post-pick query input form
├── Inspection/
│   ├── AutomationInspector.cs        # Extract element properties via UIA
│   ├── SelectorBuilder.cs            # Walk automation tree → build selector path
│   ├── ProcessDetector.cs            # Identify process type (native/browser/Electron)
│   └── ScreenshotCapture.cs          # Capture element bounding rect as PNG
├── Session/
│   ├── InspectionSession.cs          # Holds list of captured elements
│   ├── CapturedElement.cs            # Data model: element + query + screenshot
│   └── ClipboardExporter.cs          # Builds markdown report → clipboard
├── Hotkeys/
│   └── GlobalHotkeyManager.cs        # RegisterHotKey/UnregisterHotKey P/Invoke
├── Settings/
│   ├── AppSettings.cs                # Settings data model
│   └── SettingsManager.cs            # Load/save JSON from %APPDATA%
└── Interop/
    └── NativeMethods.cs              # All P/Invoke declarations (Win32 APIs)
```

### Data Flow

```
[User clicks "Pick"]
        │
        ▼
[OverlayWindow activates]
        │
        ▼ (mouse move)
[ElementPicker → UIA.FromPoint() → ElementHighlighter draws rect]
        │
        ▼ (mouse click)
[AutomationInspector extracts properties]
[SelectorBuilder walks tree → generates selector]
[ScreenshotCapture captures bounding rect → saves PNG]
[ProcessDetector identifies app type]
        │
        ▼
[QueryDialog shows → user types query → clicks Add]
        │
        ▼
[CapturedElement created → added to InspectionSession]
[TrayMenuBuilder rebuilds context menu]
[NotifyIcon tooltip updated]
        │
        ▼ (user clicks "Copy All")
[ClipboardExporter builds markdown from all CapturedElements]
[Markdown text → System clipboard]
[BalloonTip toast: "Copied N elements"]
```

---

## 7. Clipboard Output Format Specification

### Header

```markdown
# UI Inspection Report ({N} elements)
**App**: {process name} | **Captured**: {timestamp}
```

### Per Element

```markdown
## Element {N}: {ControlType} "{Name}"
- **Type**: {ControlType}
- **Name**: "{Name}"
- **AutomationId**: {AutomationId or "(none)"}
- **ClassName**: {ClassName}
- **Selector**: `{full selector path}`
- **State**: Enabled={true/false}, Visible={true/false}
- **Bounds**: x:{X} y:{Y} w:{W} h:{H}
- **Process**: {process.exe} ({detected framework})
- **Screenshot**: `{absolute path to PNG}`
- **Parent**: {ControlType} "{Name}" (AutomationId: {id})
- **Siblings**: {count} siblings ({brief list})
- **Query**: "{user's query text}"
```

### Footer

```markdown
---
*Captured by UI Inspector v1.0*
```

---

## 8. Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|-----------|------------|
| **UIA returns incomplete data** for some apps | Reduced usefulness | High | Gracefully handle null/empty properties. Show "(unavailable)" instead of crashing. Always capture screenshot as visual fallback. |
| **Pick overlay intercepts clicks** meant for other apps | User confusion | Medium | Ensure overlay is truly click-through except during active pick mode. Escape always cancels. |
| **Hotkey conflicts** with other apps | Feature unavailable | Medium | Make hotkeys configurable. Detect conflicts on registration and notify user. |
| **High-DPI / multi-monitor issues** | Incorrect element bounds, misaligned overlay | High | Use per-monitor DPI awareness. Test on mixed-DPI setups. Use `SetProcessDpiAwareness`. |
| **UIA hangs on unresponsive apps** | App freezes | Medium | Wrap all UIA calls with timeouts (2s). Run UIA calls on background thread. |
| **Large number of screenshots fills disk** | Disk space | Low | Auto-cleanup with configurable retention. Warn if folder exceeds 100 MB. |

---

## 9. Out of Scope (v1.0)

- macOS or Linux support
- MCP server integration
- Skill file generation
- Browser CDP integration (deferred to v1.1)
- Annotation/drawing on screenshots
- Recording/playback of UI interactions
- Direct source code mapping (left to the AI agent)
- Automatic AI agent integration (clipboard is the interface)

---

## 10. Success Metrics

| Metric | Target |
|--------|--------|
| Time from "Pick" to clipboard | < 10 seconds for single element |
| Clipboard output parseable by AI | AI correctly identifies element in codebase > 80% of time |
| App memory footprint (idle) | < 30 MB |
| App startup time | < 2 seconds |
| Binary size (framework-dependent) | < 10 MB |

---

## 11. Future Considerations (Post v1.0)

- **v1.1**: CDP browser inspection for richer web element data
- **v1.2**: Element tree view popup (show full automation tree for a window)
- **v1.3**: Custom clipboard format templates (users can customize markdown format)