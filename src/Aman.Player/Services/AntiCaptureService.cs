using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Aman.Player.Services;

/// <summary>
/// Best-effort screen-capture deterrents. None of this is a security
/// boundary — a phone camera pointed at the monitor, a capture card, or a
/// kernel-level tool sits entirely outside what a user-mode Windows app
/// can block. What this *does* do:
///  - <see cref="ProtectWindow"/>: on Windows 10 2004+, excludes this
///    window's content from the OS's own capture surfaces (Windows
///    Graphics Capture, and by extension most screen recorders and
///    Print Screen's window-capture mode) via SetWindowDisplayAffinity.
///  - <see cref="InstallKeyboardHook"/>: swallows the PrintScreen key at
///    the low-level keyboard hook, before it reaches the OS's
///    keyboard-triggered screenshot handler.
/// </summary>
public sealed class AntiCaptureService : IDisposable
{
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_SNAPSHOT = 0x2C; // Print Screen

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    public AntiCaptureService()
    {
        _proc = HookCallback;
    }

    public void ProtectWindow(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).EnsureHandle();
        SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
    }

    public void InstallKeyboardHook()
    {
        using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(currentModule.ModuleName!), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN && Marshal.ReadInt32(lParam) == VK_SNAPSHOT)
            return (IntPtr)1; // swallow the key — never reaches the OS's screenshot handler

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}
