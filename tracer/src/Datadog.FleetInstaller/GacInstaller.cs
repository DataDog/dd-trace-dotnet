// <copyright file="GacInstaller.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
#if NETFRAMEWORK
using System.Runtime.Remoting;
using System.Security.Permissions;
#endif
using PInvoke;

namespace Datadog.FleetInstaller;

internal static class GacInstaller
{
#if NETFRAMEWORK
    private static readonly string MsCorLibDirectory
        = Path.GetDirectoryName(Assembly.GetAssembly(typeof(object))!.Location.Replace('/', '\\'))!;
#endif

    /// <summary>
    /// Publish the tracer files to the GAC.
    /// </summary>
    public static bool TryGacInstall(ILogger log, VersionedTracerValues tracerValues)
    {
        // We use the versioned x64 native loader path as the file that should exist to avoid uninstalling from the GAC
        // while the file is still in use
        // TODO: verify this holds up in 100% of cases.

        var pairs = tracerValues
            .FilesToAddToGac
            .Select(gacFile => (gacFile, tracerValues.NativeLoaderX64Path));

        return TryGacInstall(log, pairs);
    }

    /// <summary>
    /// Remove the tracer files from the GAC.
    /// </summary>
    public static bool TryGacUninstall(ILogger log, VersionedTracerValues tracerValues)
    {
        // We use the versioned x86 native loader path as the file that should exist to avoid uninstalling from the GAC
        // while the file is still in use.
        // We additionally pre-emptively explicitly block removing from the GAC while either the x64 or x86 files are on
        // disk - if they're on disk, they could be referenced, and removing the files from the GAC is dangerous. If the
        // files are removed, we know they're not in use, so we're safe

        foreach (var nativeLoader in new[] { tracerValues.NativeLoaderX86Path, tracerValues.NativeLoaderX64Path })
        {
            if (File.Exists(nativeLoader))
            {
                log.WriteError($"Error uninstalling files from the GAC - .NET Tracer file '{nativeLoader}' still exists on disk. " +
                               $"It is not safe to remove files from the GAC.");
                return false;
            }
        }

        var pairs = tracerValues
            .FilesToAddToGac
            .Select(gacFile => (gacFile, tracerValues.NativeLoaderX64Path));

        return TryGacUninstall(log, pairs);
    }

    /// <summary>
    /// Publish an assembly to the GAC.
    /// Based on System.EnterpriseServices.Internal.Publish.GacInstall(), but
    /// ensures we add the ref counting.
    /// </summary>
    /// <param name="log">A logger instance</param>
    /// <param name="filesToGac">A collection of pairs of files to install in the GAC.
    /// <c>AssemblyToGacPath</c> is the absolute path to the assembly to add to the GAC.
    /// <c>AssociatedFilepath</c> is the path to an application that uses the GAC assembly. The item should not be removed from the GAC if this application exists</param>
    private static unsafe bool TryGacInstall(ILogger log, IEnumerable<(string AssemblyToGacPath, string AssociatedFilepath)> filesToGac)
    {
        try
        {
#if NETFRAMEWORK
            new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand();
            var fusionDllPath = Path.Combine(MsCorLibDirectory, "fusion.dll");
            if (LoadLibrary(fusionDllPath) == IntPtr.Zero)
            {
                log.WriteError($"Error loading fusion.dll from '{fusionDllPath}': path not found");
                return false;
            }
#endif

            var retValue = Fusion.CreateAssemblyCache(out var ppAsmCache, 0);
            if (retValue != HResult.Code.S_OK)
            {
                log.WriteError($"Error installing assemblies to GAC. Error code {retValue} returned from CreateAssemblyCache.");
                return false;
            }

            foreach (var (assemblyToGacPath, associatedFilepath) in filesToGac)
            {
                fixed (char* associatedFilepathPtr = associatedFilepath)
                {
                    var fusionInstallReference = new Fusion.FUSION_INSTALL_REFERENCE
                    {
                        cbSize = (uint)sizeof(Fusion.FUSION_INSTALL_REFERENCE),
                        dwFlags = Fusion.FusionInstallReferenceFlags.None,
                        guidScheme = Fusion.FUSION_INSTALL_REFERENCE.FUSION_REFCOUNT_FILEPATH_GUID,
                        szIdentifier = associatedFilepathPtr
                    };
                    retValue = ppAsmCache.InstallAssembly(0U, assemblyToGacPath, &fusionInstallReference);
                }

                if (retValue != HResult.Code.S_OK)
                {
                    log.WriteError($"Error installing assembly '{assemblyToGacPath}' to GAC. Error code {retValue} returned from CreateAssemblyCache.");
                    return false;
                }

                log.WriteInfo($"Successfully installed assembly '{assemblyToGacPath}' into the GAC.");
            }

            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, "Error installing assemblies to GAC");
            return false;
        }
    }

    /// <summary>
    /// Remove an assembly from the GAC.
    /// This should only be called if we _know_ that the assembly won't be needed anymore
    /// </summary>
    /// <param name="log">A logger instance</param>
    /// <param name="filesToRemove">A collection of pairs of files to uninstall from the GAC.
    /// <c>AssemblyToGacPath</c> is the absolute path to the assembly to add to the GAC.
    /// <c>AssociatedFilepath</c> is the path to an application that uses the GAC assembly. The item should not be removed from the GAC if this application exists</param>
    private static unsafe bool TryGacUninstall(ILogger log, IEnumerable<(string AssemblyToGacPath, string AssociatedFilepath)> filesToRemove)
    {
        try
        {
#if NETFRAMEWORK
            new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand();
#endif

            var retValue = Fusion.CreateAssemblyCache(out var ppAsmCache, 0);
            if (retValue != HResult.Code.S_OK)
            {
                log.WriteError($"Error uninstalling assemblies from GAC. Error code {retValue} returned from CreateAssemblyCache.");
                return false;
            }

            foreach (var (gacAssemblyPath, associatedFilepath) in filesToRemove)
            {
                var gacName = GetGacName(gacAssemblyPath);
                if (string.IsNullOrEmpty(gacName))
                {
                    log.WriteError($"Error uninstalling '{gacAssemblyPath}' from GAC: could not determine GAC name.");
                }

                Fusion.UninstallDisposition pulDisposition = 0;
                fixed (char* associatedFilepathPtr = associatedFilepath)
                {
                    var fusionInstallReference = new Fusion.FUSION_INSTALL_REFERENCE
                    {
                        cbSize = (uint)sizeof(Fusion.FUSION_INSTALL_REFERENCE),
                        dwFlags = Fusion.FusionInstallReferenceFlags.None,
                        guidScheme = Fusion.FUSION_INSTALL_REFERENCE.FUSION_REFCOUNT_FILEPATH_GUID,
                        szIdentifier = associatedFilepathPtr
                    };
                    retValue = ppAsmCache.UninstallAssembly(0U, gacName, &fusionInstallReference, &pulDisposition);
                }

                if (retValue != HResult.Code.S_OK)
                {
                    log.WriteError(
                        $"Error uninstalling assembly '{gacAssemblyPath}' from GAC. Error code {retValue} returned from UninstallAssembly, with uninstall value {pulDisposition}.");
                    return false;
                }

                log.WriteInfo($"Successfully uninstalled assembly '{gacAssemblyPath}' from the GAC.");
            }

            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, "Error uninstalling assemblies from the GAC");
            return false;
        }
    }

    private static string GetGacName(string fileName)
    {
#if NETFRAMEWORK
        return new AssemblyManager().GetGacName(fileName);
#else
        var assemblyName = Path.GetFileNameWithoutExtension(fileName);
        return $"{assemblyName},Version={version.ToString()}";
#endif
    }

// #if NETCOREAPP
    // [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    // private static partial IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPWStr)] string filename);
// #else
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPWStr)] string filename);
// #endif

#if NETFRAMEWORK

// Based on System.EnterpriseServices.Internal.AssemblyManager
    public class AssemblyManager : MarshalByRefObject
    {
        public string GetGacName(string fileName)
        {
            var gacName = string.Empty;
            var domain = AppDomain.CreateDomain("SoapDomain", null, new AppDomainSetup());
            if (domain != null)
            {
                try
                {
                    ObjectHandle instance = domain.CreateInstance(typeof(AssemblyManager).Assembly.FullName, typeof(AssemblyManager).FullName);
                    if (instance != null)
                    {
                        gacName = ((AssemblyManager)instance.Unwrap()).InternalGetGacName(fileName);
                    }
                }
                finally
                {
                    AppDomain.Unload(domain);
                }
            }

            return gacName;
        }

        internal string InternalGetGacName(string fileName)
        {
            var gacName = string.Empty;
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(fileName);
                gacName = $"{assemblyName.Name},Version={assemblyName.Version}";
            }
            catch (Exception ex) when (ex is not NullReferenceException or SEHException)
            {
                // only throw for above exceptions
            }

            return gacName;
        }
    }
#endif
}
