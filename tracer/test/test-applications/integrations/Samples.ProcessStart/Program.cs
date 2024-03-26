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

            if (Environment.GetEnvironmentVariable("DD_TRACE_COMMANDS_COLLECTION_ENABLED") == "true")
            {
                ProcessStartCollectionTests();
            }
            else if(Environment.GetEnvironmentVariable("DO_NOT_TRACE_PROCESS") == "1")
            {
                // don't trace this one
                try
                {
                    SampleHelpers.RunCommand("nonexisting1.exe");
                }
                // we expect this to throw because it doesn't exist, so catch the expected case 
                catch (System.Reflection.TargetInvocationException ex) when(ex.InnerException is Win32Exception) { }

                // This one should be traced as usual
                try
                {
                    Process.Start(new ProcessStartInfo("nonexisting2.exe", "arg1"));
                }
                catch (Win32Exception) { }
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
                // Test - Non-existing executable with UseShellExecute = false
                Process.Start(new ProcessStartInfo("nonexisting1-false.exe") { UseShellExecute = false });
            }
            catch (Win32Exception) { }

            try
            {
                // Test - Non-existing executable with UseShellExecute = true
                Process.Start(new ProcessStartInfo("nonexisting1-true.exe") { UseShellExecute = true });
            }
            catch (Win32Exception) { }

            try
            {
                // Test - Non-existing executable with UseShellExecute = false and Arguments = "arg1-false"
                Process.Start(new ProcessStartInfo("nonexisting2-false.exe") { UseShellExecute = false, Arguments = "arg1-false test \"quoted string\" 'simple quoted' \"with a double \\\"quote inside" });
            }
            catch (Win32Exception) { }

            try
            {
                // Test - Non-existing executable with UseShellExecute = true and Arguments = "arg1-true"
                Process.Start(new ProcessStartInfo("nonexisting2-true.exe") { UseShellExecute = true, Arguments = "arg1-true" });
            }
            catch (Win32Exception) { }

            var largeString = new string('a', 4096);

            try
            {
                // Test 4 - Non-existing executable with UseShellExecute = true and Arguments = "arg1-false" + largeString
                Process.Start(new ProcessStartInfo("nonexisting3-false.exe") { UseShellExecute = false, Arguments = "arg1-false " + largeString });
            }
            catch (Win32Exception) { }

            try
            {
                // Test - Non-existing executable with UseShellExecute = false and Arguments = "arg1-true"
                Process.Start(new ProcessStartInfo("nonexisting3-true.exe") { UseShellExecute = true, Arguments = "arg1-true " + largeString });
            }
            catch (Win32Exception) { }

#if NETCOREAPP3_1_OR_GREATER
            try
            {
                // Test - Non-existing executable with UseShellExecute = false and ArgumentList = { "arg1-false", "arg2-false" }
                Process.Start(new ProcessStartInfo("nonexisting4-false.exe") { UseShellExecute = false, ArgumentList = { "arg1-false", "arg2-false" } });
            }
            catch (Win32Exception) { }

            try
            {
                // Test - Non-existing executable with UseShellExecute = true and ArgumentList = { "arg1-true", "arg2-true" }
                Process.Start(new ProcessStartInfo("nonexisting4-true.exe") { UseShellExecute = true, ArgumentList = { "arg1-true", "arg2-true" } });
            }
            catch (Win32Exception) { }

            try
            {
                // Test - Non-existing executable with UseShellExecute = false and ArgumentList = "arg1-false " + largeString
                Process.Start(new ProcessStartInfo("nonexisting5-false.exe") { UseShellExecute = false, ArgumentList = { "arg1-false", largeString } });
            }
            catch (Win32Exception) { }

            try
            {
                // Test - Non-existing executable with UseShellExecute = true and ArgumentList = "arg1-true " + largeString
                Process.Start(new ProcessStartInfo("nonexisting5-true.exe") { UseShellExecute = true, ArgumentList = { "arg1-true", largeString } });
            }
            catch (Win32Exception) { }
#endif
        }

    }
}
