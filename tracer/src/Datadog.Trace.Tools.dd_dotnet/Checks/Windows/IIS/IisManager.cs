// <copyright file="IisManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Datadog.Trace.Tools.Shared.Windows;

namespace Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS
{
    [SupportedOSPlatform("windows")]
    internal class IisManager : IDisposable
    {
        private readonly NativeObjects.IAppHostAdminManager _appHostAdminManager;

        private IisManager(NativeObjects.IAppHostAdminManager appHostAdminManager)
        {
            _appHostAdminManager = appHostAdminManager;
        }

        public static IisManager? Create(string? applicationHostConfigurationPath)
        {
            var appHostAdminManagerGuid = Guid.Parse("228FB8F7-FB53-4FD5-8C7B-FF59DE606C5B");
            var iAppHostAdminManagerGuid = Guid.Parse("9BE77978-73ED-4A9A-87FD-13F09FEC1B13");

            int result = NativeMethods.CoCreateInstance(
                in appHostAdminManagerGuid,
                IntPtr.Zero,
                0x1,
                in iAppHostAdminManagerGuid,
                out var ptr);

            if (result != 0)
            {
                if (result == -2147221164)
                {
                    Utils.WriteError(Resources.IisNotFound);
                }
                else
                {
                    Utils.WriteError(Resources.IisManagerInitializationError(Marshal.GetPInvokeErrorMessage(result)));
                }

                return null;
            }

            var appHostAdminManager = NativeObjects.IAppHostAdminManager.Wrap(ptr);

            if (applicationHostConfigurationPath != null)
            {
                var pathMapper = new PathMapper(applicationHostConfigurationPath);
                appHostAdminManager.SetMetadata("pathMapper", pathMapper.Object);
            }

            return new(appHostAdminManager);
        }

        public void Dispose()
        {
            _appHostAdminManager.Dispose();
        }

        public Application? GetApplication(string siteName, string applicationPath)
        {
            using var section = _appHostAdminManager.GetAdminSection("system.applicationHost/sites", "MACHINE/WEBROOT/APPHOST");
            using var collection = section.Collection();

            var siteCount = collection.Count();

            for (int i = 0; i < siteCount; i++)
            {
                using var site = collection.GetItem(i);

                if (site.GetStringProperty("name") != siteName)
                {
                    continue;
                }

                using var applications = site.Collection();

                var applicationCount = applications.Count();

                for (int j = 0; j < applicationCount; j++)
                {
                    IAppHostElement? application = null;

                    try
                    {
                        application = applications.GetItem(j);

                        if (application.GetStringProperty("path") == applicationPath)
                        {
                            return new(_appHostAdminManager, application);
                        }

                        application.Dispose();
                    }
                    catch
                    {
                        application?.Dispose();
                        throw;
                    }
                }
            }

            return null;
        }

        public IReadOnlyList<string> GetApplicationNames()
        {
            var result = new List<string>();

            using var section = _appHostAdminManager.GetAdminSection("system.applicationHost/sites", "MACHINE/WEBROOT/APPHOST");
            using var collection = section.Collection();

            var siteCount = collection.Count();

            for (int i = 0; i < siteCount; i++)
            {
                using var site = collection.GetItem(i);

                var siteName = site.GetStringProperty("name");

                using var applications = site.Collection();

                var applicationCount = applications.Count();

                for (int j = 0; j < applicationCount; j++)
                {
                    using var application = applications.GetItem(j);

                    result.Add($"{siteName}{application.GetStringProperty("path")}");
                }
            }

            return result;
        }

        public IReadOnlyDictionary<string, string> GetDefaultEnvironmentVariables()
        {
            var result = new Dictionary<string, string>();

            using var applicationPoolsSection = _appHostAdminManager.GetAdminSection("system.applicationHost/applicationPools", "MACHINE/WEBROOT/APPHOST");
            using var applicationPoolDefaults = applicationPoolsSection.GetElementByName("applicationPoolDefaults");
            using var environmentVariables = applicationPoolDefaults.GetElementByName("environmentVariables");

            using var collection = environmentVariables.Collection();
            var count = collection.Count();

            for (int i = 0; i < count; i++)
            {
                using var item = collection.GetItem(i);
                result.Add(item.GetStringProperty("name"), item.GetStringProperty("value"));
            }

            return result;
        }

        private unsafe class PathMapper : IAppHostPathMapper
        {
            private static readonly Guid Guid = Guid.Parse("e7927575-5cc3-403b-822e-328a6b904bee");

            private readonly string _applicationHostConfigurationPath;

            private GCHandle _handle;
            private int _refCount;

            public PathMapper(string applicationHostConfigurationPath)
            {
                _applicationHostConfigurationPath = applicationHostConfigurationPath;

                var vtable = (IntPtr*)NativeMemory.Alloc((nuint)4, (nuint)IntPtr.Size);

                *(vtable + 0) = (IntPtr)(delegate* unmanaged<IntPtr*, Guid*, nint*, int>)&Exports.QueryInterface;
                *(vtable + 1) = (IntPtr)(delegate* unmanaged<IntPtr*, int>)&Exports.AddRef;
                *(vtable + 2) = (IntPtr)(delegate* unmanaged<IntPtr*, int>)&Exports.Release;
                *(vtable + 3) = (IntPtr)(delegate* unmanaged<IntPtr*, IntPtr, IntPtr, IntPtr*, int>)&Exports.MapPath;

                var obj = (IntPtr*)NativeMemory.Alloc((nuint)2, (nuint)IntPtr.Size);
                *obj = (IntPtr)vtable;

                _handle = GCHandle.Alloc(this);
                *(obj + 1) = GCHandle.ToIntPtr(_handle);

                Object = (IntPtr)obj;
            }

            ~PathMapper()
            {
                Dispose();
            }

            public IntPtr Object { get; private set; }

            public void Dispose()
            {
                if (Object != IntPtr.Zero)
                {
                    var target = (void**)Object;
                    NativeMemory.Free(*target);
                    NativeMemory.Free(target);
                    _handle.Free();
                    Object = IntPtr.Zero;
                }

                GC.SuppressFinalize(this);
            }

            public int QueryInterface(in Guid guid, out IntPtr ptr)
            {
                if (guid == Guid)
                {
                    AddRef();
                    ptr = Object;
                    return 0;
                }

                ptr = IntPtr.Zero;
                return unchecked((int)0x80004002); // E_NOINTERFACE
            }

            public int AddRef()
            {
                return Interlocked.Increment(ref _refCount);
            }

            public int Release()
            {
                var count = Interlocked.Decrement(ref _refCount);

                if (count == 0)
                {
                    Dispose();
                }

                return count;
            }

            public string MapPath(string virtualPath, string mappedPhysicalPath)
            {
                if (virtualPath == "MACHINE/WEBROOT/APPHOST")
                {
                    return _applicationHostConfigurationPath;
                }

                return mappedPhysicalPath;
            }

            private static class Exports
            {
                [UnmanagedCallersOnly]
                public static int QueryInterface(IntPtr* self, System.Guid* guid, nint* ptr)
                {
                    var handle = GCHandle.FromIntPtr(*(self + 1));
                    var obj = (PathMapper)handle.Target!;
                    var result = obj.QueryInterface(*guid, out var localPtr);
                    *ptr = localPtr;
                    return result;
                }

                [UnmanagedCallersOnly]
                public static int AddRef(IntPtr* self)
                {
                    var handle = GCHandle.FromIntPtr(*(self + 1));
                    var obj = (PathMapper)handle.Target!;
                    var result = obj.AddRef();
                    return result;
                }

                [UnmanagedCallersOnly]
                public static int Release(IntPtr* self)
                {
                    var handle = GCHandle.FromIntPtr(*(self + 1));
                    var obj = (PathMapper)handle.Target!;
                    var result = obj.Release();
                    return result;
                }

                [UnmanagedCallersOnly]
                public static int MapPath(IntPtr* self, IntPtr virtualPath, IntPtr mappedPhysicalPath, IntPtr* newPhysicalPath)
                {
                    var handle = GCHandle.FromIntPtr(*(self + 1));
                    var obj = (PathMapper)handle.Target!;

                    var result = obj.MapPath(Marshal.PtrToStringBSTR(virtualPath), Marshal.PtrToStringBSTR(mappedPhysicalPath));

                    *newPhysicalPath = Marshal.StringToBSTR(result);

                    return 0;
                }
            }
        }
    }
}
