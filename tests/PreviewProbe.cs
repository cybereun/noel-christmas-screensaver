using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// A hidden, dedicated test parent. No user window or system setting is touched.
internal static class PreviewProbe
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { internal int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string windowName);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();

    private static bool WaitFor(Func<bool> check, int timeout)
    {
        var watch = Stopwatch.StartNew();
        do { Application.DoEvents(); if (check()) return true; Thread.Sleep(25); } while (watch.ElapsedMilliseconds < timeout);
        return check();
    }

    private static IntPtr FindChild(IntPtr parent, uint pid)
    {
        IntPtr child = IntPtr.Zero;
        while ((child = FindWindowEx(parent, child, null, null)) != IntPtr.Zero)
        {
            uint owner; GetWindowThreadProcessId(child, out owner); if (owner == pid) return child;
        }
        return IntPtr.Zero;
    }

    private static bool SameSize(IntPtr child, IntPtr parent)
    {
        NativeRect c, p;
        return GetWindowRect(child, out c) && GetClientRect(parent, out p) && c.Right - c.Left == p.Right - p.Left && c.Bottom - c.Top == p.Bottom - p.Top;
    }

    private static void Geometry(IntPtr child, IntPtr parent)
    {
        NativeRect c, p;
        GetWindowRect(child, out c); GetClientRect(parent, out p);
        Console.WriteLine("GEOMETRY parent client=" + (p.Right - p.Left) + "x" + (p.Bottom - p.Top) + ", child outer=" + (c.Right - c.Left) + "x" + (c.Bottom - c.Top));
        try { Console.WriteLine("DPI parent=" + GetDpiForWindow(parent) + " awareness=" + GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(parent)) + ", child=" + GetDpiForWindow(child) + " awareness=" + GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(child))); } catch (EntryPointNotFoundException) { }
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        Console.WriteLine("PASS " + label);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if ((args.Length != 1 && args.Length != 2) || !File.Exists(args[0]) || (args.Length == 2 && args[1] != "--dpi-aware")) { Console.WriteLine("Usage: PreviewProbe.exe path-to-Noel.exe [--dpi-aware]"); return 2; }
        if (args.Length == 2) SetProcessDPIAware();
        Process childProcess = null;
        Form parent = null;
        try
        {
            parent = new Form { Text = "Noel isolated preview test", AutoScaleMode = AutoScaleMode.None, ClientSize = new Size(320, 180), ShowInTaskbar = false };
            IntPtr parentHandle = parent.Handle;
            childProcess = Process.Start(new ProcessStartInfo { FileName = Path.GetFullPath(args[0]), Arguments = "/p " + unchecked((ulong)parentHandle.ToInt64()).ToString(System.Globalization.CultureInfo.InvariantCulture), UseShellExecute = false, CreateNoWindow = true });
            IntPtr child = IntPtr.Zero;
            Require(WaitFor(delegate { child = FindChild(parentHandle, (uint)childProcess.Id); return child != IntPtr.Zero || childProcess.HasExited; }, 15000) && child != IntPtr.Zero, "preview creates child HWND within 15 seconds");
            Require(GetParent(child) == parentHandle, "preview is a direct child of the supplied host");
            Geometry(child, parentHandle);
            Require(WaitFor(delegate { return SameSize(child, parentHandle); }, 3000), "preview fills initial client area");
            parent.ClientSize = new Size(526, 292);
            Require(WaitFor(delegate { return SameSize(child, parentHandle); }, 3000), "preview follows parent resize");
            parent.Dispose(); parent = null;
            Require(WaitFor(delegate { return childProcess.HasExited; }, 5000), "preview process exits after parent destruction");
            Require(!IsWindow(child), "preview child HWND is destroyed");
            Require(childProcess.ExitCode == 0, "preview exits successfully");
            Console.WriteLine("7 preview integration checks passed");
            return 0;
        }
        catch (Exception e) { Console.WriteLine("FAIL " + e); return 1; }
        finally
        {
            if (parent != null) parent.Dispose();
            if (childProcess != null)
            {
                if (!WaitFor(delegate { return childProcess.HasExited; }, 2000)) childProcess.Kill();
                childProcess.Dispose();
            }
        }
    }
}
