using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;

namespace Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver
{
    public class Program
    {
        // If FALSE, must place them there manually!
        private const bool PlaceTargetAssembliesToAppDirs = false;

        static void Main(string[] args)
        {
            (new Program()).Run(args);
        }

        public void Run(string[] args)
        {
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

                        if (args.Length > 1 && args[1] != null && bool.TryParse(args[1], out bool parsedParam))
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

#pragma warning disable CS0162 // Unreachable code detected (const bool config)
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
#pragma warning restore CS0162 // Unreachable code detected

                LoadTestTargetsInCurrentAppDomain();

                const string CustomAppDomainName = "Validation non-default App Domain";
                if (!AppDomain.CurrentDomain.FriendlyName.Equals(CustomAppDomainName))
                {
                    string thisExecutableFile = Path.ChangeExtension(this.GetType().Assembly.Location, "exe");

                    ConsoleWrite.LineLine($"Will attempt to repeat the test in a custom AppDomain.");
                    ConsoleWrite.Line($"    Executable to load into the AppDomain: \"{thisExecutableFile}\".");

                    AppDomain additionalAppDomain = AppDomain.CreateDomain(CustomAppDomainName);
                    additionalAppDomain.ExecuteAssembly(thisExecutableFile);
                }

            } catch(Exception ex)
            {
                ConsoleWrite.Exception(ex);
            }

            if (AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                ConsoleWrite.LineLine("Press Enter!");
                Console.ReadKey();

                ConsoleWrite.Line("All done. Good bye.");
            }
            else
            {
                ConsoleWrite.LineLine($"{nameof(Program)} was run in a non-default AppDomain. All done. Good bye.");
            }
        }

        private void LoadTestTargetsInCurrentAppDomain()
        {
            try
            {
                ConsoleWrite.LineLine("Invoking AssemblyLoader.Run(..)");
                ConsoleWrite.Line($"    Current AD: Id={AppDomain.CurrentDomain.Id}, IsDefaultAppDomain={AppDomain.CurrentDomain.IsDefaultAppDomain()}.");

                var assemblyNamesToLoadIntoDefaultAppDomain = new string[] { "Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver.DefAd.Asm1.dll" };
                var assemblyNamesToLoadIntoNonDefaultAppDomains = new string[] { "Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver.DefAd.Asm1.dll" };
                AssemblyLoader.Run(assemblyNamesToLoadIntoDefaultAppDomain, assemblyNamesToLoadIntoNonDefaultAppDomains);

                ConsoleWrite.Line("AssemblyLoader.Run(..) invoked. Validating that target assemblies have been executed.");

                object adData = AppDomain.CurrentDomain.GetData("Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver.DefaultAD.Assembly1");
                if (adData != null && adData is string adDataString && adData.Equals("Dummy Work Performed"))
                {
                    ConsoleWrite.LineLine($"SUCCESS! Test data=\"{adDataString}\".");
                }
                else
                {
                    ConsoleWrite.LineLine($"No confirmation domain data foud! Test data type=\"{adData?.GetType()?.FullName ?? "<null>"}\".");
                }
            }
            catch(Exception ex)
            {
                ConsoleWrite.Exception(ex);
            }
        }

        private void MoveTestTargetAssemliesToAppDirs(out bool mustRelaunchAsAdmin)
        {
            MoveTestTargetAssemlyToAppDir(@"Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver.DefAd.Asm1.dll",
                                          @"c:\Program Files\Datadog\.NET Tracer\net461\",
                                          out mustRelaunchAsAdmin);

            if (mustRelaunchAsAdmin) { return; }
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
