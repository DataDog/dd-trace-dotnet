using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security;

namespace Samples.ProcessStart
{
    internal static class Program
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
        private static void Main()
        {
            Environment.SetEnvironmentVariable("PATH", "testPath");
            try
            {
                Process.Start(new ProcessStartInfo("nonexisting1.exe") { UseShellExecute = true });
            }
            catch (Win32Exception) { }

            try
            {
                Process.Start(new ProcessStartInfo("nonexisting2.exe", "arg1") { UseShellExecute = false});
            }
            catch (Win32Exception) { }

            try
            {
#if NET5_0_OR_GREATER
                Debug.Assert(OperatingSystem.IsWindows()); // this overload is only supported on Windows
#endif
                Process.Start("nonexisting3.exe", "arg1", "user", new SecureString(), "domain");
            }
            catch (Win32Exception) { }
            catch (PlatformNotSupportedException) { }           

            try
            {
#if NET5_0_OR_GREATER
                Debug.Assert(OperatingSystem.IsWindows()); // this overload is only supported on Windows
#endif
                Process.Start("nonexisting4.exe", "user", new SecureString(), "domain");
            }
            catch (Win32Exception) { }
            catch (PlatformNotSupportedException) { }

            try
            {
                Process process = new Process();
                process.StartInfo = new ProcessStartInfo("nonexisting5.exe", "args");
                process.StartInfo.UseShellExecute = false;
                process.Start();
            }
            catch (Win32Exception) { }
        }
    }
}
