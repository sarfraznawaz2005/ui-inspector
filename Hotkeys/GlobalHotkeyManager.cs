using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using UIInspector.Interop;

namespace UIInspector.Hotkeys
{
    /// <summary>
    /// Registers and manages system-wide global hotkeys using Win32
    /// <c>RegisterHotKey</c> / <c>UnregisterHotKey</c>.
    ///
    /// Subclasses <see cref="NativeWindow"/> to obtain an HWND without
    /// creating a visible window, then intercepts <c>WM_HOTKEY</c> messages
    /// in <see cref="WndProc"/> and raises <see cref="HotkeyPressed"/>.
    /// </summary>
    public sealed class GlobalHotkeyManager : NativeWindow, IDisposable
    {
        // =====================================================================
        // Events
        // =====================================================================

        /// <summary>
        /// Raised on the message-pump thread when a registered hotkey is activated.
        /// The argument is the hotkey ID that was passed to <see cref="Register"/>.
        /// </summary>
        public event Action<int>? HotkeyPressed;

        // =====================================================================
        // Private state
        // =====================================================================

        /// <summary>Maps hotkey ID → registered virtual-key code (for cleanup logging).</summary>
        private readonly Dictionary<int, uint> _registeredIds = new();

        private bool _disposed = false;

        // =====================================================================
        // Constructor
        // =====================================================================

        /// <summary>
        /// Creates the message-only window handle required by <c>RegisterHotKey</c>.
        /// </summary>
        public GlobalHotkeyManager()
        {
            // CreateHandle allocates an HWND for this NativeWindow so that
            // RegisterHotKey has a valid window to post WM_HOTKEY to.
            CreateHandle(new CreateParams());
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Parses <paramref name="hotkeyString"/> (e.g. "Ctrl+Shift+I") into a
        /// modifier bitmask and virtual-key code, then calls
        /// <c>RegisterHotKey</c> with the supplied <paramref name="id"/>.
        /// </summary>
        /// <param name="id">
        /// Application-defined identifier for this hotkey.
        /// Must be unique for this window handle (1–0xBFFF per MSDN).
        /// </param>
        /// <param name="hotkeyString">
        /// A "+" separated string such as "Ctrl+Shift+I" or "Alt+F12".
        /// Modifier tokens are case-insensitive: Ctrl, Shift, Alt, Win.
        /// The last token is the key name and is matched against <see cref="Keys"/>.
        /// </param>
        /// <returns>
        /// <c>true</c> if registration succeeded; <c>false</c> if the combination
        /// is already claimed by another process or the string could not be parsed.
        /// </returns>
        public bool Register(int id, string hotkeyString)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GlobalHotkeyManager));

            if (!TryParseHotkey(hotkeyString, out uint modifiers, out uint vk))
            {
                Debug.WriteLine(
                    $"[GlobalHotkeyManager] Could not parse hotkey string: \"{hotkeyString}\"");
                return false;
            }

            bool ok = NativeMethods.RegisterHotKey(Handle, id, modifiers, vk);

            if (ok)
            {
                _registeredIds[id] = vk;
                Debug.WriteLine(
                    $"[GlobalHotkeyManager] Registered hotkey id={id} \"{hotkeyString}\" " +
                    $"(mod=0x{modifiers:X} vk=0x{vk:X})");
            }
            else
            {
                int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                Debug.WriteLine(
                    $"[GlobalHotkeyManager] RegisterHotKey failed for id={id} " +
                    $"\"{hotkeyString}\" — Win32 error {err}");
            }

            return ok;
        }

        /// <summary>
        /// Unregisters the hotkey with the given <paramref name="id"/>.
        /// A no-op if the ID was never registered.
        /// </summary>
        public void Unregister(int id)
        {
            if (!_registeredIds.ContainsKey(id))
                return;

            NativeMethods.UnregisterHotKey(Handle, id);
            _registeredIds.Remove(id);

            Debug.WriteLine($"[GlobalHotkeyManager] Unregistered hotkey id={id}");
        }

        /// <summary>
        /// Unregisters every hotkey that was registered through this instance.
        /// </summary>
        public void UnregisterAll()
        {
            // Iterate over a copy because Unregister mutates _registeredIds.
            foreach (int id in new List<int>(_registeredIds.Keys))
                Unregister(id);
        }

        // =====================================================================
        // NativeWindow — message interception
        // =====================================================================

        /// <inheritdoc/>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                Debug.WriteLine($"[GlobalHotkeyManager] WM_HOTKEY received id={id}");
                HotkeyPressed?.Invoke(id);
                return;   // Message consumed — do not pass to base.
            }

            base.WndProc(ref m);
        }

        // =====================================================================
        // IDisposable
        // =====================================================================

        /// <summary>
        /// Unregisters all hotkeys and destroys the message window.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            UnregisterAll();
            DestroyHandle();
        }

        // =====================================================================
        // Private — hotkey string parsing
        // =====================================================================

        /// <summary>
        /// Parses a hotkey string such as "Ctrl+Shift+I" into Win32 modifier flags
        /// and a virtual-key code.
        /// </summary>
        /// <param name="hotkeyString">The string to parse.</param>
        /// <param name="modifiers">Receives the combined MOD_* bitmask.</param>
        /// <param name="vk">Receives the virtual-key code.</param>
        /// <returns><c>true</c> on success.</returns>
        private static bool TryParseHotkey(
            string hotkeyString,
            out uint modifiers,
            out uint vk)
        {
            modifiers = 0;
            vk        = 0;

            if (string.IsNullOrWhiteSpace(hotkeyString))
                return false;

            string[] tokens = hotkeyString.Split('+');

            if (tokens.Length < 1)
                return false;

            // The last token is the key; everything before it is a modifier.
            string keyToken = tokens[tokens.Length - 1].Trim();

            for (int i = 0; i < tokens.Length - 1; i++)
            {
                switch (tokens[i].Trim().ToUpperInvariant())
                {
                    case "CTRL":
                        modifiers |= NativeMethods.MOD_CONTROL;
                        break;
                    case "SHIFT":
                        modifiers |= NativeMethods.MOD_SHIFT;
                        break;
                    case "ALT":
                        modifiers |= NativeMethods.MOD_ALT;
                        break;
                    case "WIN":
                        modifiers |= NativeMethods.MOD_WIN;
                        break;
                    default:
                        Debug.WriteLine(
                            $"[GlobalHotkeyManager] Unknown modifier token: \"{tokens[i].Trim()}\"");
                        return false;
                }
            }

            // Map the key token to a Keys enum value (case-insensitive).
            if (!Enum.TryParse<Keys>(keyToken, ignoreCase: true, out Keys parsedKey))
            {
                Debug.WriteLine(
                    $"[GlobalHotkeyManager] Unknown key token: \"{keyToken}\"");
                return false;
            }

            vk = (uint)parsedKey;
            return true;
        }
    }
}
