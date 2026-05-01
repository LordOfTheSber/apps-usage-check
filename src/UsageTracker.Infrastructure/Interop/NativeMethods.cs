using System.Runtime.InteropServices;

namespace UsageTracker.Infrastructure.Interop;

internal static partial class NativeMethods
{
    [DllImport("user32.dll", SetLastError = false)]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = false)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
