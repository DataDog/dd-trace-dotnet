using System;
using System.Diagnostics;
using System.IO;
public class GenerationCli
{
    public static int Main(string[] args)
    {
        if (args.Length == 1 && (args[0] == "--info" || args[0] == "--version" ||
            args[0] == "--list-sdks" || args[0] == "--list-runtimes"))
        {
            var info = new ProcessStartInfo(@"C:\dotnet\dotnet.exe", args[0]);
            info.UseShellExecute = false;
            using (var process = Process.Start(info))
            {
                process.WaitForExit();
                return process.ExitCode;
            }
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("BDN_DIAGNOSTIC_GENERATION_MARKER"),
            args.Length > 0 ? args[0] : "no-arguments");
        Console.Error.WriteLine("BDN_DIAGNOSTIC_GENERATION_STOP");
        return 87;
    }
}
