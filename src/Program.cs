using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

[assembly: AssemblyTitle("Noel Christmas Screensaver")]
[assembly: AssemblyDescription("A quiet Christmas, in champagne light and falling snow")]
[assembly: AssemblyCompany("cybereun")]
[assembly: AssemblyProduct("Noel Christmas Screensaver")]
[assembly: AssemblyCopyright("Copyright © 2026 cybereun")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Noel
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var options = LaunchOptions.Parse(args);
            if (options.Mode == LaunchMode.Invalid) return;
            if (options.Mode == LaunchMode.Preview)
            {
                if (!Native.IsWindow(options.Parent)) return;
                try { Native.SetThreadDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(options.Parent)); }
                catch (EntryPointNotFoundException) { }
            }
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                try { Directory.CreateDirectory(SettingsStore.DirectoryPath); File.WriteAllText(Path.Combine(SettingsStore.DirectoryPath, "error.log"), e.Exception.ToString()); } catch { }
                e.Handled = true;
                if (options.Mode == LaunchMode.Settings || options.Mode == LaunchMode.Windowed)
                    MessageBox.Show("화면을 열지 못했습니다.\n" + e.Exception.Message, "Noël", MessageBoxButton.OK, MessageBoxImage.Error);
                app.Shutdown(1);
            };
            var settings = SettingsStore.Load(SettingsStore.FilePath);
            if (options.Mode == LaunchMode.Snapshot)
            {
                try { Snapshot(settings, options.Theme, options.OutputPath); } catch { Environment.ExitCode = 1; }
                return;
            }
            if (options.Mode == LaunchMode.Preview)
            {
                if (!Native.IsWindow(options.Parent)) return;
                using (var host = new PreviewHost(options.Parent, settings)) app.Run();
                return;
            }
            if (options.Mode == LaunchMode.Saver)
            {
                using (var run = new SaverRun(settings)) { run.Show(); app.Run(); }
                return;
            }
            if (options.Mode == LaunchMode.Windowed)
            {
                var window = CreateWindowed(settings);
                window.Closed += delegate { app.Shutdown(); };
                app.Run(window); return;
            }
            var config = new SettingsWindow(settings);
            if (options.Parent != IntPtr.Zero && Native.IsWindow(options.Parent))
                new WindowInteropHelper(config).Owner = options.Parent;
            config.Closed += delegate { app.Shutdown(); };
            app.Run(config);
        }

        internal static Window CreateWindowed(SaverSettings settings)
        {
            var scene = new WinterScene(settings.Copy());
            var window = new Window { Title = "Noël · 크리스마스 미리보기", Width = 1280, Height = 760, MinWidth = 480, MinHeight = 360,
                WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = Brushes.Black, Content = scene };
            window.Loaded += delegate { scene.Start(); };
            window.Closed += delegate { scene.Stop(); };
            window.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) window.Close(); };
            return window;
        }

        private static void Snapshot(SaverSettings settings, string path)
        {
            var scene = new WinterScene(settings);
            scene.Width = 1920; scene.Height = 1080;
            scene.Measure(new Size(1920, 1080)); scene.Arrange(new Rect(0, 0, 1920, 1080)); scene.UpdateLayout(); scene.RenderAt(24);
            var image = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32); image.Render(scene);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            string full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full));
            using (var file = File.Create(full)) encoder.Save(file);
            scene.Stop();
        }

        private static void Snapshot(SaverSettings settings, int theme, string path)
        {
            if (theme >= 0) settings.Theme = theme;
            Snapshot(settings, path);
        }

        internal static string InstallAndApply(SaverSettings settings, string runningPath)
        {
            settings.Normalize();
            string installDirectory = Path.Combine(SettingsStore.DirectoryPath, "Installed");
            string destination = Path.Combine(installDirectory, "Noel.scr");
            Directory.CreateDirectory(installDirectory);
            if (!SameFile(runningPath, destination))
            {
                try { File.Copy(runningPath, destination, true); }
                catch (IOException)
                {
                    string versioned = Path.Combine(installDirectory, "Noel-" + ShortHash(runningPath) + ".scr");
                    if (!SameFile(runningPath, versioned)) File.Copy(runningPath, versioned, true);
                    destination = versioned;
                }
            }
            SettingsStore.Save(settings, SettingsStore.FilePath);
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Control Panel\\Desktop"))
            {
                if (key == null) throw new IOException("Windows 화면 보호기 설정을 열 수 없습니다.");
                key.SetValue("SCRNSAVE.EXE", destination, RegistryValueKind.String);
                key.SetValue("ScreenSaveActive", "1", RegistryValueKind.String);
            }
            if (!Native.SystemParametersInfo(0x0011, 1, IntPtr.Zero, 0x0002))
                throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "Windows가 화면 보호기 변경을 적용하지 못했습니다.");
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", false))
            {
                if (key == null || !String.Equals(Convert.ToString(key.GetValue("SCRNSAVE.EXE")), destination, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Windows에서 선택한 화면 보호기 경로를 확인하지 못했습니다.");
            }
            return destination;
        }

        private static bool SameFile(string first, string second)
        {
            if (!File.Exists(first) || !File.Exists(second)) return false;
            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream a = new FileStream(first, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (FileStream b = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    byte[] ah = sha.ComputeHash(a), bh = sha.ComputeHash(b);
                    if (ah.Length != bh.Length) return false;
                    for (int i = 0; i < ah.Length; i++) if (ah[i] != bh[i]) return false;
                    return true;
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static string ShortHash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash, 0, 6).Replace("-", "").ToLowerInvariant();
            }
        }

        internal static void OpenScreenSaverSettings()
        {
            string control = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "control.exe");
            Process.Start(new ProcessStartInfo(control, "desk.cpl,screensaver,@screensaver") { UseShellExecute = true });
        }
    }

    internal sealed class SaverRun : IDisposable
    {
        private readonly List<Window> windows = new List<Window>();
        private readonly List<WinterScene> scenes = new List<WinterScene>();
        private readonly DispatcherTimer timer = new DispatcherTimer();
        private readonly Stopwatch clock = new Stopwatch();
        private Native.Point baseline;
        private bool armed, finished;
        private readonly Action onClosed;
        private Window primaryWindow;
        internal SaverRun(SaverSettings settings, Action callback = null)
        {
            onClosed = callback;
            foreach (var screen in Forms.Screen.AllScreens)
            {
                var bounds = screen.Bounds;
                var scene = new WinterScene(settings.Copy());
                var window = new Window { Title = "Noël · 화면 보호기", WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize, ShowInTaskbar = screen.Primary, Topmost = true,
                    Background = Brushes.Black, Cursor = Cursors.None, Content = scene,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0,
                    Width = 800, Height = 600 };
                window.SourceInitialized += delegate
                {
                    Native.SetWindowPos(new WindowInteropHelper(window).Handle, new IntPtr(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0040);
                };
                window.PreviewKeyDown += delegate { Finish(); };
                window.PreviewMouseDown += delegate { if (clock.Elapsed.TotalSeconds > 0.4) Finish(); };
                window.PreviewMouseWheel += delegate { if (clock.Elapsed.TotalSeconds > 0.4) Finish(); };
                window.Closed += delegate { if (!finished) Finish(); };
                windows.Add(window); scenes.Add(scene);
                if (screen.Primary) primaryWindow = window;
            }
            timer.Interval = TimeSpan.FromMilliseconds(80); timer.Tick += Tick;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaysChanged;
        }
        internal void Show()
        {
            for (int i = 0; i < windows.Count; i++) { windows[i].Show(); scenes[i].Start(); }
            if (primaryWindow != null) primaryWindow.Activate();
            else if (windows.Count > 0) windows[0].Activate();
            Native.GetCursorPos(out baseline); clock.Start(); timer.Start();
        }
        private void Tick(object sender, EventArgs e)
        {
            Native.Point p;
            if (!Native.GetCursorPos(out p)) return;
            if (clock.Elapsed.TotalSeconds < 1.2) { baseline = p; return; }
            if (!armed) { baseline = p; armed = true; }
            if (Math.Abs((long)p.X - baseline.X) > 6 || Math.Abs((long)p.Y - baseline.Y) > 6) { Finish(); return; }
            uint pid;
            IntPtr active = Native.GetForegroundWindow();
            if (active != IntPtr.Zero) { Native.GetWindowThreadProcessId(active, out pid); if (pid != (uint)Process.GetCurrentProcess().Id) Finish(); }
        }
        private void DisplaysChanged(object sender, EventArgs e) { Application.Current.Dispatcher.BeginInvoke(new Action(Finish)); }
        private void Finish()
        {
            if (finished) return; finished = true; Dispose();
            foreach (Window window in windows) window.Close();
            if (onClosed != null) onClosed(); else Application.Current.Shutdown();
        }
        public void Dispose()
        {
            timer.Stop(); timer.Tick -= Tick; clock.Stop();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
            foreach (var scene in scenes) scene.Stop();
        }
    }

    internal sealed class PreviewHost : IDisposable
    {
        private readonly IntPtr parent;
        private readonly HwndSource source;
        private readonly WinterScene scene;
        private readonly DispatcherTimer timer = new DispatcherTimer();
        private int width, height;
        private bool disposed;
        internal PreviewHost(IntPtr parentHandle, SaverSettings settings)
        {
            parent = parentHandle;
            Native.Rect bounds; Native.GetClientRect(parent, out bounds);
            width = Math.Max(1, bounds.Right - bounds.Left); height = Math.Max(1, bounds.Bottom - bounds.Top);
            var options = new HwndSourceParameters("Noel Screensaver Preview", width, height);
            options.ParentWindow = parent; options.WindowStyle = 0x40000000 | 0x10000000 | 0x02000000; options.PositionX = 0; options.PositionY = 0;
            source = new HwndSource(options);
            scene = new WinterScene(settings.Copy()); source.RootVisual = scene; scene.Start();
            source.Disposed += SourceDisposed;
            timer.Interval = TimeSpan.FromMilliseconds(300); timer.Tick += Tick; timer.Start();
        }
        private void SourceDisposed(object sender, EventArgs e) { Dispose(); Application.Current.Shutdown(); }
        private void Tick(object sender, EventArgs e)
        {
            if (!Native.IsWindow(parent)) { Dispose(); Application.Current.Shutdown(); return; }
            Native.Rect bounds; if (!Native.GetClientRect(parent, out bounds)) return;
            int w = Math.Max(1, bounds.Right - bounds.Left), h = Math.Max(1, bounds.Bottom - bounds.Top);
            if (w != width || h != height) { width = w; height = h; Native.SetWindowPos(source.Handle, IntPtr.Zero, 0, 0, w, h, 0x0014); }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            timer.Stop(); timer.Tick -= Tick; scene.Stop(); source.Disposed -= SourceDisposed; source.Dispose();
        }
    }
}
