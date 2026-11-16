using System;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;

namespace Noel
{
    public sealed class SaverSettings
    {
        public int SnowAmount { get; set; }
        public int Theme { get; set; }
        public bool ShowGreeting { get; set; }
        public bool ShowClock { get; set; }
        public int FrameRate { get; set; }
        public SaverSettings() { SnowAmount = 1; ShowGreeting = true; ShowClock = false; FrameRate = 30; }
        public SaverSettings Copy() { return (SaverSettings)MemberwiseClone(); }
        public void Normalize()
        {
            if (SnowAmount < 0 || SnowAmount > 2) SnowAmount = 1;
            if (Theme < 0 || Theme > 4) Theme = 0;
            if (FrameRate != 24 && FrameRate != 30 && FrameRate != 60) FrameRate = 30;
        }
    }

    internal static class SettingsStore
    {
        internal static string DirectoryPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoelScreensaver"); } }
        internal static string FilePath { get { return Path.Combine(DirectoryPath, "settings.xml"); } }
        internal static SaverSettings Load(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var result = (SaverSettings)new XmlSerializer(typeof(SaverSettings)).Deserialize(stream);
                    if (result == null) return new SaverSettings();
                    result.Normalize(); return result;
                }
            }
            catch (Exception e)
            {
                if (!(e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)) throw;
                return new SaverSettings();
            }
        }
        internal static void Save(SaverSettings settings, string path)
        {
            settings.Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temp)) new XmlSerializer(typeof(SaverSettings)).Serialize(stream, settings);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    internal enum LaunchMode { Settings, Saver, Preview, Windowed, Snapshot, Invalid }
    internal sealed class LaunchOptions
    {
        internal LaunchMode Mode = LaunchMode.Settings;
        internal IntPtr Parent = IntPtr.Zero;
        internal string OutputPath;
        internal int Theme;
        internal static LaunchOptions Parse(string[] args)
        {
            var result = new LaunchOptions();
            if (args.Length == 0) return result;
            string raw = args[0].Trim();
            if (raw == "--windowed") { result.Mode = LaunchMode.Windowed; return result; }
            if (raw == "--snapshot")
            {
                result.Mode = args.Length == 2 ? LaunchMode.Snapshot : LaunchMode.Invalid;
                if (args.Length == 2) { result.OutputPath = args[1]; result.Theme = -1; }
                return result;
            }
            if (raw == "--snapshot-theme")
            {
                int theme = -1;
                result.Mode = args.Length == 3 && Int32.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out theme) && theme >= 0 && theme <= 4 ? LaunchMode.Snapshot : LaunchMode.Invalid;
                if (result.Mode == LaunchMode.Snapshot) { result.Theme = theme; result.OutputPath = args[2]; }
                return result;
            }
            string[] parts = raw.ToLowerInvariant().Split(new char[] { ':' }, 2);
            string command = parts[0];
            if (command == "/s" || command == "-s") result.Mode = LaunchMode.Saver;
            else if (command == "/c" || command == "-c")
            {
                result.Mode = LaunchMode.Settings;
                string value = parts.Length == 2 ? parts[1].Trim() : (args.Length > 1 ? args[1].Trim() : "");
                ulong handle;
                if (UInt64.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out handle) &&
                    handle != 0 && (IntPtr.Size == 8 || handle <= UInt32.MaxValue)) result.Parent = new IntPtr(unchecked((long)handle));
            }
            else if (command == "/p" || command == "-p")
            {
                string value = parts.Length == 2 ? parts[1].Trim() : (args.Length > 1 ? args[1].Trim() : "");
                ulong handle;
                if (!UInt64.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out handle) || handle == 0 ||
                    (IntPtr.Size == 4 && handle > UInt32.MaxValue)) result.Mode = LaunchMode.Invalid;
                else { result.Mode = LaunchMode.Preview; result.Parent = new IntPtr(unchecked((long)handle)); }
            }
            else result.Mode = LaunchMode.Invalid;
            return result;
        }
    }
}
