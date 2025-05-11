using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
// Use alias for the Windows Input namespace to avoid ambiguity
using WinInput = System.Windows.Input;
using System.Windows.Forms;

namespace AutoDesktopApplication.Services
{
    /// <summary>
    /// Service to hook global keyboard events
    /// </summary>
    public class KeyboardHookService : IDisposable
    {
        // Keyboard hook ID
        private IntPtr _hookId = IntPtr.Zero;

        // Delegate types for keyboard events with simple int parameter for virtual key code
        public delegate void KeyDownEventHandler(object sender, int vkCode);
        public delegate void KeyUpEventHandler(object sender, int vkCode);

        // Events that can be subscribed to
        public event KeyDownEventHandler? KeyDown;
        public event KeyUpEventHandler? KeyUp;
        
        // Add specific event for the escape key
        public event EventHandler? EscapeKeyPressed;

        // Win32 constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        
        // Virtual key code for Escape
        private const int VK_ESCAPE = 0x1B;

        // Keyboard hook callback delegate
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private readonly LowLevelKeyboardProc _proc;

        // Flag to track if hook is active
        private bool _isHookActive = false;

        /// <summary>
        /// Constructor to initialize the keyboard hook callback
        /// </summary>
        public KeyboardHookService()
        {
            _proc = HookCallback;
        }

        /// <summary>
        /// Starts the keyboard hook
        /// </summary>
        public void StartHook()
        {
            if (_isHookActive)
                return;

            HookKeyboard();
            _isHookActive = true;
        }

        /// <summary>
        /// Stops the keyboard hook
        /// </summary>
        public void StopHook()
        {
            if (!_isHookActive || _hookId == IntPtr.Zero)
                return;

            try
            {
                UnhookWindowsHookEx(_hookId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unhooking keyboard hook: {ex.Message}");
            }
            finally
            {
                _hookId = IntPtr.Zero;
                _isHookActive = false;
            }
        }

        /// <summary>
        /// Sets up the keyboard hook
        /// </summary>
        private void HookKeyboard()
        {
            using (Process curProcess = Process.GetCurrentProcess())
            {
                ProcessModule? curModule = curProcess.MainModule;
                if (curModule != null)
                {
                    _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
                }
                else
                {
                    Debug.WriteLine("Error: Could not get main module.");
                }
            }
        }

        /// <summary>
        /// Callback method for the keyboard hook
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    // Raise key down event with virtual key code
                    KeyDown?.Invoke(this, vkCode);

                    // Check for Escape key press
                    if (vkCode == VK_ESCAPE)
                    {
                        EscapeKeyPressed?.Invoke(this, EventArgs.Empty);
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    // Raise key up event with virtual key code
                    KeyUp?.Invoke(this, vkCode);
                }
            }

            // Call the next hook in the chain
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Clean up hooks when service is disposed
        /// </summary>
        public void Dispose()
        {
            StopHook();
        }

        // Win32 API imports
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, 
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}