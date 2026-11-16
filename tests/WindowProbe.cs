using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal static class WindowProbe
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { internal int Left, Top, Right, Bottom; public override string ToString() { return Left + "," + Top + " " + (Right - Left) + "x" + (Bottom - Top); } }
    private delegate bool EnumProc(IntPtr hwnd, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLong64(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int result, int size);
    private static long Long(IntPtr hwnd, int index) { return IntPtr.Size == 8 ? GetWindowLong64(hwnd, index).ToInt64() : GetWindowLong32(hwnd, index); }

    [STAThread]
    private static int Main(string[] args)
    {
        uint wanted;
        if (args.Length != 1 || !UInt32.TryParse(args[0], out wanted)) { Console.WriteLine("Usage: WindowProbe.exe PID"); return 2; }
        SetProcessDPIAware();
        foreach (Screen screen in Screen.AllScreens) Console.WriteLine("MONITOR " + screen.DeviceName + " primary=" + screen.Primary + " bounds=" + screen.Bounds);
        IntPtr foreground = GetForegroundWindow(); uint foregroundPid; GetWindowThreadProcessId(foreground, out foregroundPid);
        Console.WriteLine("FOREGROUND hwnd=" + foreground + " PID=" + foregroundPid);
        int count = 0;
        EnumWindows(delegate(IntPtr hwnd, IntPtr unused)
        {
            uint pid; uint tid = GetWindowThreadProcessId(hwnd, out pid); if (pid != wanted) return true;
            count++;
            var name = new StringBuilder(1024); var cls = new StringBuilder(512); GetWindowText(hwnd, name, name.Capacity); GetClassName(hwnd, cls, cls.Capacity);
            Rect rect, client; GetWindowRect(hwnd, out rect); GetClientRect(hwnd, out client);
            int cloaked; DwmGetWindowAttribute(hwnd, 14, out cloaked, 4);
            uint dpi = 0; try { dpi = GetDpiForWindow(hwnd); } catch (EntryPointNotFoundException) { }
            Console.WriteLine("HWND=" + hwnd + " TID=" + tid + " visible=" + IsWindowVisible(hwnd) + " minimized=" + IsIconic(hwnd) + " cloaked=" + cloaked + " dpi=" + dpi + " style=0x" + Long(hwnd, -16).ToString("X") + " exstyle=0x" + Long(hwnd, -20).ToString("X") + " rect=" + rect + " client=" + client + " title=" + name + " class=" + cls);
            return true;
        }, IntPtr.Zero);
        Console.WriteLine(count + " top-level windows");
        var process = Process.GetProcessById((int)wanted);
        Console.WriteLine("PROCESS responding=" + process.Responding + " CPU=" + process.TotalProcessorTime + " workingSet=" + process.WorkingSet64);
        foreach (ProcessThread thread in process.Threads)
        {
            if (thread.TotalProcessorTime.TotalSeconds > 1) Console.WriteLine("THREAD " + thread.Id + " CPU=" + thread.TotalProcessorTime + " state=" + thread.ThreadState);
        }
        return 0;
    }
}
