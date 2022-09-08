using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Threading.Tasks;

namespace Samples.ProcessStart
{
    internal static class Program
    {
        private static void Main()
        {
            Environment.SetEnvironmentVariable("PATH", "testPath");
            try
            {
                Process.Start(new ProcessStartInfo("nonexisting1.exe") { UseShellExecute = false });
            }
            catch (Win32Exception) { }

            try
            {
                Process.Start(new ProcessStartInfo("nonexisting2.exe", "arg1") { UseShellExecute = false});
            }
            catch (Win32Exception) { }

            try
            {
                Process.Start("nonexisting3.exe", "arg1", "user", new SecureString(), "domain");
            }
            catch (Win32Exception) { }
            catch (PlatformNotSupportedException) { }           

            try
            {
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
