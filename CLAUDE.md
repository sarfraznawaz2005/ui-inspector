# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

UIInspector is a Windows system tray application that inspects UI elements for AI coding agents. It uses UI Automation to detect elements at screen coordinates, capture screenshots, build CSS-like selectors, and export element metadata as markdown to the clipboard.

## Build & Run Commands

```bash
# Debug build
dotnet build

# Run in development
dotnet run

# Release publish (self-contained single EXE)
dotnet publish -c Release -r win-x64 /p:PublishingBinary=true

# Or use the PowerShell build script
./build.ps1 -Mode self-contained    # default, single portable EXE
./build.ps1 -Mode framework-dependent
```

Published output: `dist/UIInspector.exe`

There are no automated tests — testing is manual (pick elements across WinForms, WPF, Chromium, Electron, WebView2 apps).

## Tech Stack

- .NET 8.0 (`net8.0-windows`), C#, no external NuGet packages
- Hybrid WinForms (tray icon, dialogs) + WPF (transparent overlay window)
- Win32 P/Invoke for global hotkeys and low-level mouse/keyboard hooks
- `System.Windows.Automation` for UI element inspection
- GDI+ (`System.Drawing`) for screenshot capture
- `System.Text.Json` for settings persistence

## Architecture

The app runs as a WinForms `ApplicationContext` — its lifetime is tied to the tray icon, not a visible window. Single-instance is enforced via a named Mutex.

### Module Responsibilities

| Directory | Role |
|-----------|------|
| `Tray/` | App context (`TrayApplication`), context menu, icon generation |
| `Picker/` | Picking session: WPF overlay, highlight drawing, low-level hooks, query dialog |
| `Inspection/` | UI Automation inspection, selector building, screenshot capture, process classification |
| `Session/` | Captured element storage, clipboard markdown export |
| `Hotkeys/` | Global hotkey registration via hidden `NativeWindow` |
| `Settings/` | JSON settings model, load/save with atomic writes |
| `Interop/` | P/Invoke declarations for Win32 APIs |

### Core Workflow

`TrayApplication` orchestrates everything. When the user triggers a pick (via hotkey or menu):

1. **ElementPicker** shows a transparent WPF overlay, installs low-level mouse/keyboard hooks, and polls cursor position (50ms interval, 5px movement threshold)
2. **AutomationInspector** inspects the element at the cursor with a 2-second timeout (runs on background thread to avoid UI hangs)
3. **ElementHighlighter** draws a rectangle on the overlay around the detected element
4. On left-click, **SelectorBuilder** walks the automation tree to build a selector path and gather parent/sibling context
5. **ScreenshotCapture** captures the element region with 8px padding and a 2px highlight border
6. **QueryDialog** optionally collects a user annotation
7. **InspectionSession** stores the `CapturedElement` (1-based indices, never reused after deletion)
8. **ClipboardExporter** auto-copies markdown if enabled

### Threading Model

- WPF overlay runs on its own `DispatcherThread` (separate from the WinForms message loop)
- UI Automation calls run on a background thread with `CancellationTokenSource` timeout
- Process type cache uses locks; screenshot sequence counter uses locks
- Session mutations assume single UI thread ownership

### Settings & Data Paths

- Settings: `%APPDATA%\UIInspector\settings.json` (atomic write: temp file + rename)
- Screenshots: configurable, default `%TEMP%\ui-inspector` (auto-cleaned after 24h)
- Error log: `%APPDATA%\UIInspector\error.log` (truncated at 1 MB)

## Key Design Decisions

- **No external dependencies**: Everything uses .NET Framework libraries for single-EXE portability
- **WinForms + WPF hybrid**: WinForms for tray/dialogs, WPF for the overlay (WPF handles window transparency better)
- **DPI declared in both** `app.manifest` and `.csproj` `ApplicationHighDpiMode` — the WFAC010 warning is intentionally suppressed
- **PublishSingleFile/SelfContained** are only active during `dotnet publish` with `/p:PublishingBinary=true` to avoid conflicts when the app is running during development builds
