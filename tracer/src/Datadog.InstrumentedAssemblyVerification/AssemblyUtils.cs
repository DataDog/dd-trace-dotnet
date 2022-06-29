using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Datadog.InstrumentedAssemblyVerification
{
    internal static class AssemblyUtils
    {
        internal static Assembly LoadAssemblyFromAndLogFailure(string assemblyPath, InstrumentationVerificationLogger logger)
        {
            try
            {
                return Assembly.LoadFrom(assemblyPath);
            }
            catch (BadImageFormatException e) when (e.Message.ToLower().Contains("bad binary signature"))
            {
                logger.Error($"Failed to load {assemblyPath} assembly. {e.Message}{Environment.NewLine}This could indicate that the assembly have some invalid metadata signature");
                throw;
            }
            catch (BadImageFormatException e) when (e.Message.ToLower().Contains("bad il format"))
            {
                logger.Error($"Failed to load output assembly. {e.Message}{Environment.NewLine}This could indicate that the assembly is R2R module, a native host executable or there is an architecture mismatch");
                throw;
            }
            catch (BadImageFormatException e)
            {
                logger.Error($"Failed to load {assemblyPath} assembly. {e.Message}{Environment.NewLine}This could indicate on x64-x86 conflict");
                throw;
            }
        }

        internal static bool IsNetFramework(Assembly assembly)
        {
            string framework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

            if (string.IsNullOrEmpty(framework))
            {
                return assembly.GetCustomAttributes<AssemblyMetadataAttribute>().
                                Any(att => att.Key.Equals(".NETFrameworkAssembly", StringComparison.InvariantCultureIgnoreCase)) ||
                assembly.GetReferencedAssemblies().Any(reference => reference.Name == "mscorlib");
            }

            return framework.StartsWith(".NETFramework", StringComparison.InvariantCultureIgnoreCase);
        }

        internal static IEnumerable<string> GetDefaultWindowsGacPaths()
        {
            string environmentVariable = Environment.GetEnvironmentVariable("WINDIR");
            if (environmentVariable != null)
            {
                yield return Path.Combine(environmentVariable, Path.Combine("Microsoft.NET", "assembly"));
                yield return Path.Combine(environmentVariable, "assembly");
                yield return Path.Combine(environmentVariable, "Microsoft.NET");
            }
        }

        internal static string GetProgramFilesPath(bool preferX64 = true)
        {
            if (preferX64)
            {
                return Environment.GetEnvironmentVariable("ProgramW6432");
            }

            return Environment.GetEnvironmentVariable("ProgramFiles(x86)") ??
                   Environment.GetEnvironmentVariable("ProgramFiles");
        }

        internal static Assembly GetAssemblyFromGac(List<string> gacPaths, string assemblyName)
        {
            string[] gacNames = new[] { "GAC_MSIL", "GAC_64", "GAC_32", "GAC", "Framework", "Framework64" };
            string[] prefixes = new[] { "v4.0", string.Empty };

            // It's not a good solution because I'm not considering version and signature, but I'll save it to later
            string gacAssembly = (from gacPath in gacPaths
                                  //from prefix in prefixes
                                  from gacName in gacNames
                                  let gac = Path.Combine(gacPath, gacName)
                                  let assemblyDir = Path.Combine(gac, assemblyName)
                                  where Directory.Exists(assemblyDir)
                                  from directory in Directory.EnumerateDirectories(assemblyDir)
                                  from file in Directory.EnumerateFiles(directory, "*.dll")
                                  where Path.GetFileNameWithoutExtension(file) == assemblyName
                                  select file).FirstOrDefault();

            if (gacAssembly == null)
            {
                gacAssembly = (from gacPath in gacPaths
                               from gacName in gacNames
                               let gac = Path.Combine(gacPath, gacName)
                               where Directory.Exists(gac)
                               from file in Directory.EnumerateFiles(gac, "*.dll", SearchOption.AllDirectories)
                               where Path.GetFileNameWithoutExtension(file) == assemblyName
                               select file).FirstOrDefault();
            }

            if (!string.IsNullOrEmpty(gacAssembly) && File.Exists(gacAssembly))
            {
                return Assembly.LoadFile(gacAssembly);
            }

            return null;
        }

        internal static Assembly GetAssemblyFromNugetFolder(string assemblyName, Version assemblyVersion, bool isNetCore)
        {
            string nugetPackagesFolder = GetNugetGlobalPackagesFolder();
            if (nugetPackagesFolder == null)
            {
                return null;
            }
            
            string assemblyFolder = Path.Combine(nugetPackagesFolder, assemblyName);
            if (Directory.Exists(assemblyFolder) == false)
            {
                return null;
            }

            var assemblyVersionsFolders = Directory.EnumerateDirectories(assemblyFolder).ToList();
            if (assemblyVersionsFolders.Count == 0)
            {
                return null;
            }

            string majorAndMinorVersion = $"{assemblyVersion.Major}.{assemblyVersion.Minor}";
            var matchedMajorAndMinorVersion = assemblyVersionsFolders.Where(folder => folder.StartsWith(majorAndMinorVersion)).ToList();

            string assemblyVersionFolder;
            switch (matchedMajorAndMinorVersion.Count)
            {
                case 0:
                    var matchedMajorVersion = assemblyVersionsFolders.Where(folder =>
                        folder.StartsWith(assemblyVersion.Major.ToString())).ToList();
                    assemblyVersionFolder = matchedMajorVersion.Any() ? matchedMajorVersion.Max() : assemblyVersionsFolders.Max();
                    break;
                case 1:
                    assemblyVersionFolder = matchedMajorAndMinorVersion[0];
                    break;
                default:
                    assemblyVersionFolder = matchedMajorAndMinorVersion.Max();
                    break;
            }

            string assemblyFile = null;
            foreach (string directory in Directory.EnumerateDirectories(Path.Combine(assemblyVersionFolder, "lib")))
            {
                if (isNetCore && Path.GetDirectoryName(directory)?.StartsWith("netstandard") == true)
                {
                    assemblyFile = directory;
                    break;
                }
                if (!isNetCore && Path.GetDirectoryName(directory)?.StartsWith("netstandard") == false)
                {
                    assemblyFile = directory;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(assemblyFile))
            {
                assemblyFile = Path.Combine(assemblyFile, assemblyName + ".dll");
                if (File.Exists(assemblyFile))
                {
                    return Assembly.LoadFile(assemblyFile);
                }
            }
            return null;
        }

        private static string GetNugetGlobalPackagesFolder()
        {
            var nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (nugetPackages != null)
            {
                return nugetPackages;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.ExpandEnvironmentVariables(@"%userprofile%\.nuget\packages");
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return @"~/.nuget/packages";
            }
            return null;
        }

        internal static bool IsCoreClr()
        {
            return RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework") == false;
        }
    }
}
