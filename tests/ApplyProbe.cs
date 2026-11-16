using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Win32;

// Explicit, per-user Windows apply check. Run only when an actual apply is requested.
internal static class ApplyProbe
{
    private const BindingFlags StaticHidden = BindingFlags.Static | BindingFlags.NonPublic;
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !File.Exists(args[0])) { Console.Error.WriteLine("Usage: ApplyProbe.exe dist\\Noel.exe"); return 2; }
        try
        {
            Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            Type settingsType = assembly.GetType("Noel.SaverSettings", true);
            Type storeType = assembly.GetType("Noel.SettingsStore", true);
            Type appType = assembly.GetType("Noel.Program", true);
            MethodInfo load = storeType.GetMethod("Load", StaticHidden);
            MethodInfo apply = appType.GetMethod("InstallAndApply", StaticHidden);
            MethodInfo open = appType.GetMethod("OpenScreenSaverSettings", StaticHidden);
            string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoelScreensaver", "settings.xml");
            object settings = load.Invoke(null, new object[] { settingsPath });
            string installDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoelScreensaver", "Installed");
            Console.WriteLine("Applying theme " + settingsType.GetProperty("Theme").GetValue(settings, null));
            Console.WriteLine("Install directory: " + installDirectory);
            Console.WriteLine("Build assembly: " + assembly.Location);
            PrintConstants(apply);
            string timeoutBefore, secureBefore;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", false))
            {
                timeoutBefore = Convert.ToString(key.GetValue("ScreenSaveTimeOut", ""));
                secureBefore = Convert.ToString(key.GetValue("ScreenSaverIsSecure", ""));
            }
            object result = apply.Invoke(null, new object[] { settings, Path.GetFullPath(args[0]) });
            string expected = Path.GetFullPath(Convert.ToString(result));
            Console.WriteLine("Installed file: " + expected);
            if (!expected.StartsWith(Path.GetFullPath(installDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Installed .scr is outside the app install directory");
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", false))
            {
                string selected = Convert.ToString(key.GetValue("SCRNSAVE.EXE"));
                if (!String.Equals(selected, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Windows did not keep the selected screen saver");
                if (Convert.ToString(key.GetValue("ScreenSaveActive")) != "1") throw new InvalidOperationException("Screen saver is not enabled");
                if (Convert.ToString(key.GetValue("ScreenSaveTimeOut", "")) != timeoutBefore || Convert.ToString(key.GetValue("ScreenSaverIsSecure", "")) != secureBefore) throw new InvalidOperationException("Windows timeout/security options unexpectedly changed");
            }
            if (!File.Exists(expected)) throw new InvalidOperationException("Installed screen saver file is missing");
            open.Invoke(null, new object[0]);
            Console.WriteLine("PASS applied theme " + settingsType.GetProperty("Theme").GetValue(settings, null));
            Console.WriteLine("PASS Windows selected " + expected);
            Console.WriteLine("PASS Windows screen saver settings opened");
            Console.WriteLine("PASS existing timeout and secure/resume preferences preserved");
            return 0;
        }
        catch (TargetInvocationException e)
        {
            Console.Error.WriteLine(e.InnerException ?? e); return 1;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private static void PrintConstants(MethodInfo method)
    {
        byte[] il = method.GetMethodBody().GetILAsByteArray();
        var codes = new System.Collections.Generic.Dictionary<short, OpCode>();
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.FieldType == typeof(OpCode)) { OpCode op = (OpCode)field.GetValue(null); codes[op.Value] = op; }
        int p = 0;
        while (p < il.Length)
        {
            short code = il[p++] == 0xfe ? (short)(0xfe00 | il[p++]) : il[p - 1];
            OpCode op = codes[code];
            if (op.OperandType == OperandType.InlineString)
            {
                int token = BitConverter.ToInt32(il, p); p += 4;
                Console.WriteLine("IL string: " + method.Module.ResolveString(token));
            }
            else if (op.OperandType == OperandType.InlineSwitch) { int n = BitConverter.ToInt32(il, p); p += 4 + n * 4; }
            else
            {
                switch (op.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: p += 1; break;
                    case OperandType.InlineVar: p += 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: p += 8; break;
                    case OperandType.InlineI: case OperandType.InlineBrTarget: case OperandType.InlineField:
                    case OperandType.InlineMethod: case OperandType.InlineSig: case OperandType.InlineTok:
                    case OperandType.InlineType: case OperandType.ShortInlineR: p += 4; break;
                    default: throw new InvalidOperationException("Unexpected IL operand " + op.OperandType);
                }
            }
        }
    }
}
