// <copyright file="LibraryLoader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf.Initialization
{
    internal static class LibraryLoader
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LibraryLoader));

        internal static IntPtr LoadAndGetHandle()
        {
            var fd = FrameworkDescription.Create();

            GetLibNameAndRuntimeId(fd, out var libName, out var runtimeId);

            // libName or runtimeId being null means platform is not supported
            // no point attempting to load the library
            if (libName != null && runtimeId != null)
            {
                var paths = GetDatadogNativeFolders(fd, runtimeId);
                if (TryLoadLibraryFromPaths(libName, paths, out var handle))
                {
                    return handle;
                }
            }

            return IntPtr.Zero;
        }

        private static List<string> GetDatadogNativeFolders(FrameworkDescription frameworkDescription, string runtimeId)
        {
            // first get anything "home folder" like
            // if running under Windows:
            // - get msi install location
            // - then get the default program files folder (because of a know issue in the installer for location of the x86 folder on a 64bit OS)
            // then combine with the profiler's location
            // taking into account that these locations could be the same place

            var paths = GetHomeFolders(runtimeId);

            if (frameworkDescription.OSPlatform == OSPlatform.Windows)
            {
                var programFilesFolder = GetProgramFilesFolder();
                paths.Add(programFilesFolder);

                AddPathFromMsiSettings(paths);
            }

            var profilerFolder = GetProfilerFolder(frameworkDescription);
            if (!string.IsNullOrWhiteSpace(profilerFolder))
            {
                paths.Add(profilerFolder);
            }
            else
            {
                // this is expected under Windows, but problematic under other OSs
                Log.Debug("Couldn't find profilerFolder");
            }

            return paths.Distinct().ToList();
        }

        private static void AddPathFromMsiSettings(List<string> paths)
        {
            void AddInstallDirFromRegKey(string bitness)
            {
                var path = $@"SOFTWARE\Datadog\Datadog .NET Tracer {bitness}-bit";
                var installDir = ReducedRegistryAccess.ReadLocalMachineString(path, "InstallPath");
                if (installDir != null)
                {
                    paths.Add(installDir);
                }
            }

            if (Environment.Is64BitOperatingSystem)
            {
                AddInstallDirFromRegKey("64");
            }

            AddInstallDirFromRegKey("32");
        }

        private static string GetProgramFilesFolder()
        {
            // should be already adapted to the type of process / OS
            var programFilesRoot = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            return Path.Combine(programFilesRoot, "Datadog", ".NET Tracer");
        }

        private static string GetProfilerFolder(FrameworkDescription frameworkDescription)
        {
            var profilerEnvVar =
                frameworkDescription.IsCoreClr() ? "CORECLR_PROFILER_PATH" : "COR_PROFILER_PATH";
            var profilerEnvVarBitsExt =
                profilerEnvVar + (Environment.Is64BitProcess ? "_64" : "_32");

            var profilerPathsEnvVars = new List<string>()
            {
                profilerEnvVarBitsExt, profilerEnvVar
            };

            // it is unlikely that the security library would be in a sub folder from
            // where the profiler lives, so just use this paths directly
            var profilerFolders =
                profilerPathsEnvVars
                   .Select(Environment.GetEnvironmentVariable)
                   .Where(x => !string.IsNullOrWhiteSpace(x))
                   .Select(Path.GetDirectoryName)
                   .FirstOrDefault();

            return profilerFolders;
        }

        private static List<string> GetHomeFolders(string runtimeId)
        {
            List<string> GetRuntimeSpecificVersions(string path)
            {
                return new List<string>()
                {
                    path, Path.Combine(path, runtimeId), Path.Combine(path, runtimeId, "native"),
                };
            }

            // treat any path that could contain integrations.json as home folder
            var integrationsPaths = Environment.GetEnvironmentVariable("DD_INTEGRATIONS")
                                              ?.Split(';')
                                              ?.Where(x => !string.IsNullOrWhiteSpace(x))
                                              ?.Select(Path.GetDirectoryName)
                                              ?.ToList()
                                 ?? new List<string>();

            // the real trace home
            var tracerHome = Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");
            if (!string.IsNullOrWhiteSpace(tracerHome))
            {
                integrationsPaths.Add(tracerHome);
            }

            // include the appdomain base as this will help framework samples running in IIS find the library
            var currentDir =
                string.IsNullOrEmpty(AppDomain.CurrentDomain.RelativeSearchPath) ?
                    AppDomain.CurrentDomain.BaseDirectory : AppDomain.CurrentDomain.RelativeSearchPath;

            if (!string.IsNullOrWhiteSpace(currentDir))
            {
                integrationsPaths.Add(currentDir);
                Log.Debug("currentDir is {CurrentDir}", currentDir);
            }
            else
            {
                Log.Debug("currentDir is null or empty");
            }

            // the home folder could contain the native dll directly, but it could also
            // be under a runtime specific folder
            var paths =
                integrationsPaths
                   .Distinct()
                   .SelectMany(GetRuntimeSpecificVersions)
                   .ToList();

            return paths;
        }

        private static bool TryLoadLibraryFromPaths(string libName, List<string> paths, out IntPtr handle)
        {
            var success = false;
            handle = IntPtr.Zero;

            foreach (var path in paths)
            {
                var libFullPath = Path.Combine(path, libName);

                if (!File.Exists(libFullPath))
                {
                    continue;
                }

                var loaded = NativeLibrary.TryLoad(libFullPath, out handle);

                if (loaded)
                {
                    success = true;
                    Log.Information($"Loaded library '{libName}' from '{path}' with handle '{handle}'");
                    break;
                }

                Log.Warning($"Failed to load library '{libName}' from '{path}'");
            }

            if (!success)
            {
                Log.Warning($"Failed to load library '{libName}' from any of the following '{string.Join(", ", paths)}'");
                Log.Error("AppSec could not load libddwaf native library, as a result, AppSec could not start. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help.");
            }

            return success;
        }

        private static bool IsMuslBasedLinux()
        {
            var muslDistros = new[]
            {
                "alpine"
            };

            var files =
                Directory.GetFiles("/etc", "*release")
                         .Concat(Directory.GetFiles("/etc", "*version"))
                         .ToList();

            if (File.Exists("/etc/issue"))
            {
                files.Add("/etc/issue");
            }

            return files
                  .Select(File.ReadAllText)
                  .Any(fileContents => muslDistros.Any(distroId => fileContents.ToLowerInvariant().Contains(distroId)));
        }

        private static void GetLibNameAndRuntimeId(FrameworkDescription frameworkDescription, out string libName, out string runtimeId)
        {
            string runtimeIdPart1, libPrefix, libExt;

            switch (frameworkDescription.OSPlatform)
            {
                case OSPlatform.MacOS:
                    runtimeIdPart1 = "osx";
                    libPrefix = "lib";
                    libExt = "dylib";
                    break;
                case OSPlatform.Linux:
                    runtimeIdPart1 =
                        IsMuslBasedLinux() ?
                            "linux-musl" :
                            "linux";
                    libPrefix = "lib";
                    libExt = "so";
                    break;
                case OSPlatform.Windows:
                    runtimeIdPart1 = "win";
                    libPrefix = string.Empty;
                    libExt = "dll";
                    break;
                default:
                    // unsupported platform
                    runtimeIdPart1 = null;
                    libPrefix = null;
                    libExt = null;
                    break;
            }

            if (runtimeIdPart1 != null && libPrefix != null && libExt != null)
            {
                libName = libPrefix + "ddwaf." + libExt;
                runtimeId = Environment.Is64BitProcess ? runtimeIdPart1 + "-x64" : runtimeIdPart1 + "-x86";
            }
            else
            {
                Log.Warning($"Unsupported platform: " + Environment.OSVersion.Platform);

                libName = null;
                runtimeId = null;
            }
        }
    }
}
