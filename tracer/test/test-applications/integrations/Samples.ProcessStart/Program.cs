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

            if (Environment.GetEnvironmentVariable("DD_COMMANDS_COLLECTION_ENABLED") == "true")
            {
                ProcessStartCollectionTests();
            }
            else
            {
                ProcessStartTests();
            }
        }
        
        private static void ProcessStartTests()
        {
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
                if (OperatingSystem.IsWindows())
                {
#endif
                    Process.Start("nonexisting3.exe", "arg1", "user", new SecureString(), "domain");
#if NET5_0_OR_GREATER
                }
#endif
            }
            catch (Win32Exception) { }
            catch (PlatformNotSupportedException) { }           

            try
            {
#if NET5_0_OR_GREATER
                if (OperatingSystem.IsWindows())
                {
#endif
                    Process.Start("nonexisting4.exe", "user", new SecureString(), "domain");
#if NET5_0_OR_GREATER
                }
#endif
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

        private static void ProcessStartCollectionTests()
        {
            try
            {
                // Test - Non-existing executable with UseShellExecute = true
                Process.Start(new ProcessStartInfo("nonexisting1.exe") { UseShellExecute = true });
            }
            catch (Win32Exception) { }

            try
            {
                // Test - Non-existing executable with UseShellExecute = false
                Process.Start(new ProcessStartInfo("nonexisting1.exe") { UseShellExecute = false });
            }
            catch (Win32Exception) { }

            try
            {
                // Test - Non-existing executable with UseShellExecute = true and Arguments = "arg1"
                Process.Start(new ProcessStartInfo("nonexisting2.exe") { UseShellExecute = true, Arguments = "arg1" });
            }
            catch (Win32Exception) { }

            try
            {
                // Test - Non-existing executable with UseShellExecute = false and Arguments = "arg2"
                Process.Start(new ProcessStartInfo("nonexisting2.exe") { UseShellExecute = false, Arguments = "arg2" });
            }
            catch (Win32Exception) { }

            var largeString = new string('a', 4096);
            
            try
            {
                // Test - Non-existing executable with UseShellExecute = false and Arguments = "arg2"
                Process.Start(new ProcessStartInfo("nonexisting3.exe") { UseShellExecute = false, Arguments = "arg1 " + largeString });
            }
            catch (Win32Exception) { }
            
            try
            {
                // Test 4 - Non-existing executable with UseShellExecute = true and Arguments = "arg1" + largeString
                Process.Start(new ProcessStartInfo("nonexisting3.exe") { UseShellExecute = true, Arguments = "arg1 " + largeString });
            }
            catch (Win32Exception) { }

#if NETFRAMEWORK || NETSTANDARD2_0
            try
            {
                // Test - Non-existing executable with UseShellExecute = true and ArgumentList = { "arg1", "arg2" }
                Process.Start(new ProcessStartInfo("nonexisting1.exe") { UseShellExecute = true, ArgumentList = { "arg1", "arg2" } });
            }
            catch (Win32Exception) { }

            try
            {
                // Test - Non-existing executable with UseShellExecute = false and ArgumentList = { "arg3", "arg4" }
                Process.Start(new ProcessStartInfo("nonexisting1.exe") { UseShellExecute = false, ArgumentList = { "arg3", "arg4" } });
            }
            catch (Win32Exception) { }
            
            try
            {
                // Test - Non-existing executable with UseShellExecute = true and ArgumentList = "arg1 " + largeString
                Process.Start(new ProcessStartInfo("nonexisting3.exe") { UseShellExecute = false, ArgumentList = { "arg1", largeString } });
            }
            catch (Win32Exception) { }
            
            try
            {
                // Test - Non-existing executable with UseShellExecute = false and ArgumentList = "arg1 " + largeString
                Process.Start(new ProcessStartInfo("nonexisting3.exe") { UseShellExecute = false, ArgumentList = { "arg3", largeString } });
            }
            catch (Win32Exception) { }
#endif
        }

    }
}
