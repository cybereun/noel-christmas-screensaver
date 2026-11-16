using System;
using System.IO;
using Noel;

internal static class CoreTests
{
    private static int passed, failed;
    private static string sandbox;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }

    private static void Mode(LaunchMode expected, params string[] args)
    {
        Check(LaunchOptions.Parse(args).Mode == expected, "Unexpected launch mode");
    }

    [STAThread]
    private static int Main()
    {
        sandbox = Path.Combine(Path.GetTempPath(), "NoelCoreTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            Test("no arguments opens settings", delegate { Mode(LaunchMode.Settings); });
            Test("screensaver switch and case", delegate { Mode(LaunchMode.Saver, "/s"); Mode(LaunchMode.Saver, "/S"); Mode(LaunchMode.Saver, "-s"); });
            Test("settings switch and owner syntax", delegate { Mode(LaunchMode.Settings, "/c"); Mode(LaunchMode.Settings, "/C:1234"); Mode(LaunchMode.Settings, "/c", "1234"); });
            Test("preview split handle", delegate { var o = LaunchOptions.Parse(new string[] { "/p", "1234567" }); Check(o.Mode == LaunchMode.Preview && o.Parent.ToInt64() == 1234567, "Preview handle not preserved"); });
            Test("preview colon handle and whitespace", delegate { var o = LaunchOptions.Parse(new string[] { " /P: 1234567 " }); Check(o.Mode == LaunchMode.Preview && o.Parent.ToInt64() == 1234567, "Colon preview handle not preserved"); });
            Test("preview missing handle rejected", delegate { Mode(LaunchMode.Invalid, "/p"); Mode(LaunchMode.Invalid, "/p:"); });
            Test("preview zero and signed handle rejected", delegate { Mode(LaunchMode.Invalid, "/p", "0"); Mode(LaunchMode.Invalid, "/p", "-1"); Mode(LaunchMode.Invalid, "/p", "+1234"); });
            Test("preview invalid and overflowing decimal rejected", delegate { Mode(LaunchMode.Invalid, "/p", "garbage"); Mode(LaunchMode.Invalid, "/p", "18446744073709551616"); Mode(LaunchMode.Invalid, "/p", "0x1234"); });
            Test("preview pointer width is respected", delegate { var o = LaunchOptions.Parse(new string[] { "/p", "4294967296" }); Check(o.Mode == (IntPtr.Size == 8 ? LaunchMode.Preview : LaunchMode.Invalid), "Pointer width check failed"); });
            Test("unknown option rejected", delegate { Mode(LaunchMode.Invalid, "/unknown"); });
            Test("windowed launch", delegate { Mode(LaunchMode.Windowed, "--windowed"); });
            Test("snapshot requires exactly one output argument", delegate { Mode(LaunchMode.Invalid, "--snapshot"); Mode(LaunchMode.Invalid, "--snapshot", "a.png", "extra"); var o = LaunchOptions.Parse(new string[] { "--snapshot", "a b.png" }); Check(o.Mode == LaunchMode.Snapshot && o.OutputPath == "a b.png", "Snapshot output was changed"); var five = LaunchOptions.Parse(new string[] { "--snapshot-theme", "4", "five.png" }); Check(five.Mode == LaunchMode.Snapshot && five.Theme == 4 && five.OutputPath == "five.png", "Theme render option failed"); Mode(LaunchMode.Invalid, "--snapshot-theme", "5", "bad.png"); });
            Test("new settings sensible defaults and five scene options", delegate { var s = new SaverSettings(); Check(s.SnowAmount == 1 && s.Theme == 0 && s.ShowGreeting && !s.ShowClock && s.FrameRate == 30, "Unexpected defaults"); for (int i = 0; i < 5; i++) { var selected = new SaverSettings { Theme = i }; selected.Normalize(); Check(selected.Theme == i, "Theme choice was not preserved"); } });
            Test("normalization rejects invalid values", delegate { var s = new SaverSettings { SnowAmount = -1, Theme = -1, FrameRate = 0 }; s.Normalize(); Check(s.SnowAmount == 1 && s.Theme == 0 && s.FrameRate == 30, "Invalid settings not normalized"); s.SnowAmount = 3; s.Theme = 5; s.FrameRate = 144; s.Normalize(); Check(s.SnowAmount == 1 && s.Theme == 0 && s.FrameRate == 30, "High settings not normalized"); });
            Test("normalization preserves supported values", delegate { for (int theme = 0; theme < 5; theme++) foreach (int snow in new int[] { 0, 1, 2 }) foreach (int fps in new int[] { 24, 30, 60 }) { var s = new SaverSettings { Theme = theme, SnowAmount = snow, FrameRate = fps, ShowClock = true, ShowGreeting = false }; s.Normalize(); Check(s.Theme == theme && s.SnowAmount == snow && s.FrameRate == fps && s.ShowClock && !s.ShowGreeting, "Supported value changed"); } });
            Test("settings copy is independent", delegate { var source = new SaverSettings(); var copy = source.Copy(); copy.SnowAmount = 2; Check(source.SnowAmount == 1 && !Object.ReferenceEquals(source, copy), "Copy aliases original"); });
            Test("missing settings use defaults", delegate { var s = SettingsStore.Load(Path.Combine(sandbox, "missing.xml")); Check(s.SnowAmount == 1 && s.ShowGreeting && s.FrameRate == 30, "Missing settings not defaulted"); });
            Test("malformed settings use defaults", delegate { string path = Path.Combine(sandbox, "bad.xml"); File.WriteAllText(path, "<broken>"); var s = SettingsStore.Load(path); Check(s.SnowAmount == 1 && s.ShowGreeting, "Malformed settings not defaulted"); });
            Test("null XML settings use defaults", delegate { string path = Path.Combine(sandbox, "null.xml"); File.WriteAllText(path, "<SaverSettings xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\" />"); var s = SettingsStore.Load(path); Check(s != null && s.FrameRate == 30, "Null settings not defaulted"); });
            Test("out-of-range persisted settings normalize on load", delegate { string path = Path.Combine(sandbox, "range.xml"); File.WriteAllText(path, "<SaverSettings><SnowAmount>99</SnowAmount><FrameRate>900</FrameRate><ShowClock>true</ShowClock></SaverSettings>"); var s = SettingsStore.Load(path); Check(s.SnowAmount == 1 && s.FrameRate == 30 && s.ShowClock, "Persisted invalid values not normalized"); });
            Test("save creates directories and roundtrips", delegate { string path = Path.Combine(sandbox, "nested", "settings.xml"); SettingsStore.Save(new SaverSettings { Theme = 4, SnowAmount = 2, FrameRate = 24, ShowClock = true, ShowGreeting = false }, path); var s = SettingsStore.Load(path); Check(s.Theme == 4 && s.SnowAmount == 2 && s.FrameRate == 24 && s.ShowClock && !s.ShowGreeting, "Roundtrip lost values"); });
            Test("save replaces existing file without temporary leftovers", delegate { string path = Path.Combine(sandbox, "replace.xml"); SettingsStore.Save(new SaverSettings(), path); SettingsStore.Save(new SaverSettings { SnowAmount = 0, FrameRate = 60 }, path); var s = SettingsStore.Load(path); Check(s.SnowAmount == 0 && s.FrameRate == 60, "Replacement lost values"); Check(Directory.GetFiles(sandbox, "*.tmp", SearchOption.AllDirectories).Length == 0, "Temporary files leaked"); });
        }
        finally
        {
            string full = Path.GetFullPath(sandbox);
            string prefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + "NoelCoreTests-";
            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(full)) Directory.Delete(full, true);
        }
        Console.WriteLine(passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
