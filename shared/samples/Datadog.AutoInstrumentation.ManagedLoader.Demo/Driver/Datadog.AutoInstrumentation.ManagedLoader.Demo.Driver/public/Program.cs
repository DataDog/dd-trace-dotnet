using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver
{
    public class Program
    {
        // If FALSE, must place them there manually!
        internal static readonly bool PlaceTargetAssembliesToAppDirs = false;  // static readonly rather than const to avoid unreachable code warings

        public static void Main(string[] args)
        {
            (new Program()).Run(args);
        }

        public void Run(string[] args)
        {
            bool validationSuccess = true;

            try
            {
                ConsoleWrite.Line();
                ConsoleWrite.Line($"Welcome to {this.GetType().FullName} in {Process.GetCurrentProcess().ProcessName}");
                ConsoleWrite.Line($"    BCL Assembly: \"{typeof(object).Assembly.FullName}\" is located in \"{typeof(object).Assembly.Location}\".");
                ConsoleWrite.Line($"    Environment.Version: \"{Environment.Version}\".");
                ConsoleWrite.Line($"    AssemblyFileVersion of BCL Assembly: \"{typeof(object).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version}\".");
                ConsoleWrite.Line($"    Process Id:  {Process.GetCurrentProcess().Id}");

                bool placeTargetAssembliesToAppDirs = PlaceTargetAssembliesToAppDirs;
                if (args != null && args.Length > 0)
                {
                    if (args[0] != null && args[0].Equals(nameof(PlaceTargetAssembliesToAppDirs), StringComparison.OrdinalIgnoreCase))
                    {
                        ConsoleWrite.LineLine($"Command line parameter {nameof(PlaceTargetAssembliesToAppDirs)} was specified.");

                        if (args.Length > 1 && args[1] != null && Boolean.TryParse(args[1], out bool parsedParam))
                        {
                            ConsoleWrite.Line($"    Command line parameter {nameof(PlaceTargetAssembliesToAppDirs)} parsed to be \"{parsedParam}\". Will use that value.");
                            placeTargetAssembliesToAppDirs = parsedParam;
                        }
                        else
                        {
                            ConsoleWrite.Line($"    Command line parameter value for {nameof(PlaceTargetAssembliesToAppDirs)} is not specified or cannot be parsed."
                                            + $" Will default to \"{placeTargetAssembliesToAppDirs}\".");
                        }
                    }
                }

                ConsoleWrite.LineLine($"placeTargetAssembliesToAppDirs = {placeTargetAssembliesToAppDirs}");
                if (placeTargetAssembliesToAppDirs)
                {
                    MoveTestTargetAssemliesToAppDirs(out bool mustRelaunchAsAdmin);

                    if (mustRelaunchAsAdmin)
                    {
                        ForkAsAdmin();
                        return;
                    }
                }

                validationSuccess = validationSuccess && LoadTestTargetsInCurrentAppDomain();

                try
                {
                    const string CustomAppDomainName = "Non-default Validation-App-Domain";
                    if (!AppDomain.CurrentDomain.FriendlyName.Equals(CustomAppDomainName))
                    {
                        string thisExecutableFile = Path.ChangeExtension(this.GetType().Assembly.Location, "exe");

                        ConsoleWrite.LineLine($"Will attempt to repeat the test in a custom AppDomain.");
                        ConsoleWrite.Line($"    Executable to load into the AppDomain: \"{thisExecutableFile}\".");

                        AppDomain additionalAppDomain = AppDomain.CreateDomain(CustomAppDomainName);
                        additionalAppDomain.ExecuteAssembly(thisExecutableFile);

                        validationSuccess = validationSuccess && IsDummyWorkPerformed(additionalAppDomain, out _);
                    }
                }
                catch (PlatformNotSupportedException pnsEx)
                {
                    if ("System.Private.CoreLib".Equals(typeof(object).Assembly?.GetName()?.Name, StringComparison.Ordinal))
                    {
                        ConsoleWrite.LineLine($"While running on .NET Core, encountered a {nameof(PlatformNotSupportedException)} while trying to"
                                             + " validate in non-default AppDomain. This is extected and benign becasue such AppDomains are not supported under .NET Core."
                                             + " Displaying exception details below for info only.");
                        ConsoleWrite.Exception(pnsEx);
                    }
                    else
                    {
                        ExceptionDispatchInfo.Capture(pnsEx).Throw();
                        throw;  // line never reached
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.Exception(ex);
                validationSuccess = false;
            }

            if (AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                if (validationSuccess)
                {
                    ConsoleWrite.LineLine("SUCCESS! Both, default (and non-default, if supported) AppDomain(s) validated.");
                }
                else
                {
                    ConsoleWrite.LineLine("FAILURE! Somethign did not get validated. See above output for details.");
                }

                ConsoleWrite.LineLine("Press Enter!");
                Console.ReadKey();

                ConsoleWrite.Line("All done. Good bye.");
            }
            else
            {
                ConsoleWrite.LineLine($"{nameof(Program)} was run in a non-default AppDomain. Good bye.");
            }
        }

        private bool LoadTestTargetsInCurrentAppDomain()
        {
            try
            {
                ConsoleWrite.LineLine("Invoking AssemblyLoader.Run(..)");
                ConsoleWrite.Line($"    Current AD: Id={AppDomain.CurrentDomain.Id}, IsDefaultAppDomain={AppDomain.CurrentDomain.IsDefaultAppDomain()}.");

                string[] assemblyNamesToLoadIntoDefaultAppDomain = new string[] { "Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver.DefAd.Asm1.dll" };
                string[] assemblyNamesToLoadIntoNonDefaultAppDomains = new string[] { "Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver.DefAd.Asm1.dll" };
                AssemblyLoader.Run(assemblyNamesToLoadIntoDefaultAppDomain, assemblyNamesToLoadIntoNonDefaultAppDomains);

                ConsoleWrite.Line("AssemblyLoader.Run(..) invoked. Validating that target assemblies have been executed.");

                if (IsDummyWorkPerformed(AppDomain.CurrentDomain, out string details))
                {
                    ConsoleWrite.LineLine($"AppDomain \"{AppDomain.CurrentDomain.FriendlyName}\" vaidated. Details: {details}.");
                    return true;
                }
                else
                {
                    ConsoleWrite.LineLine($"AppDomain \"{AppDomain.CurrentDomain.FriendlyName}\" not vaidated. Details: {details}.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.Exception(ex);

                ConsoleWrite.LineLine($"Error validating AppDomain.");
                return false;
            }
        }

        private static bool IsDummyWorkPerformed(AppDomain appDomain, out string details)
        {
            if (appDomain == null)
            {
                details = "Specified AppDomain is null";
                return false;
            }

            const string TestDataKey = "Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver.DefaultAD.Assembly1";
            const string TestDataExpectedValue = "Dummy Work Performed";
            object adData = appDomain.GetData(TestDataKey);
            if (adData == null)
            {
                details = $"Target AppDomain has no Data under the expected key (\"{TestDataKey}\").";
                return false;
            }

            if (!(adData is string adDataString))
            {
                details = $"Target AppDomain has Data under the expected key, however the type is {adData.GetType().FullName} whereas {typeof(string).FullName} was expected.";
                return false;
            }

            if (!adData.Equals(TestDataExpectedValue))
            {
                details = $"Target AppDomain has string Data under the expected key, however the value is \"{adData}\" whereas \"{TestDataExpectedValue}\" was expected.";
                return false;
            }

            details = $"Test data=\"{adDataString}\".";
            return true;
        }

        private void MoveTestTargetAssemliesToAppDirs(out bool mustRelaunchAsAdmin)
        {
            MoveTestTargetAssemlyToAppDir(@"Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver.DefAd.Asm1.dll",
                                          @"c:\Program Files\Datadog\.NET Tracer\net461\",
                                          out mustRelaunchAsAdmin);
        }

        private void MoveTestTargetAssemlyToAppDir(string assemblyFileName, string appDir, out bool mustRelaunchAsAdmin)
        {
            ConsoleWrite.LineLine($"Moving \"{assemblyFileName}\" to \"{appDir}\"...");

            string destination = Path.Combine(appDir, assemblyFileName);

            try
            {
                ConsoleWrite.Line($"    Making sure that \"{appDir}\" exists...");
                Directory.CreateDirectory(appDir);

                if (File.Exists(destination))
                {
                    ConsoleWrite.Line($"    \"{assemblyFileName}\" already exists. Deleting...");
                    File.Delete(destination);
                }
                else
                {
                    ConsoleWrite.Line($"    \"{assemblyFileName}\" does not yet exist; copy should be OK.");
                }

                ConsoleWrite.Line($"    Executing file move from \"{assemblyFileName}\" to \"{destination}\"...");
                File.Move(assemblyFileName, destination);

                ConsoleWrite.Line($"    Completed moving file.");
                mustRelaunchAsAdmin = false;
            }
            catch (UnauthorizedAccessException uaEx)
            {
                ConsoleWrite.Line($"    Encountered an {nameof(UnauthorizedAccessException)} ({uaEx.Message}). Need admin priviledges.");
                mustRelaunchAsAdmin = true;
                return;
            }
            catch (Exception ex)
            {
                ConsoleWrite.Exception(ex);
                mustRelaunchAsAdmin = false;
            }
        }

        private void ForkAsAdmin()
        {
            //if (IsRunAsAdmin())
            //{
            //    ConsoleWrite.Line("This program has been started with administrator priviledges.");
            //    return true;
            //}

            ConsoleWrite.LineLine("This program must be run as an administrator!");
            ConsoleWrite.Line("Relaunching...");
            ConsoleWrite.Line();

            var process = new Process();
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
            process.StartInfo.FileName = Path.ChangeExtension(Assembly.GetEntryAssembly().CodeBase, "exe");
            process.StartInfo.Verb = "runas";

            try
            {
                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                ConsoleWrite.Exception(ex);
            }
        }

        //private bool IsRunAsAdmin()
        //{
        //    WindowsIdentity id = WindowsIdentity.GetCurrent();
        //    WindowsPrincipal principal = new WindowsPrincipal(id);

        //    return principal.IsInRole(WindowsBuiltInRole.Administrator);
        //}
    }
}
