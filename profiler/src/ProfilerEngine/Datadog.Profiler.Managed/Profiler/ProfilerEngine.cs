// <copyright file="ProfilerEngine.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Configuration;
using Datadog.PProf.Export;
using Datadog.Util;

namespace Datadog.Profiler
{
    public sealed class ProfilerEngine : IDisposable
    {
        private const string LogSourceMoniker = nameof(ProfilerEngine);

#if DEBUG
        private const string BuildConfigurationMoniker = "Debug";
#else
        private const string BuildConfigurationMoniker = "Release";
#endif
        private static readonly object _singletonAccessLock = new object();

        private static ProfilerEngine _currentEngine;
        private readonly NativeInterop.ManagedCallbackRegistry.EnqueueStackSnapshotBufferSegmentForExport.Delegate_t _enqueueStackSnapshotBufferSegmentForExport;
        private readonly NativeInterop.ManagedCallbackRegistry.TryShutdownCurrentManagedProfilerEngine.Delegate_t _tryShutdownCurrentManagedProfilerEngine;
        private readonly ProfiledThreadInfoProvider _profiledThreadInfoProvider;
        private readonly ProfiledAppDomainProvider _profiledAppDomainProvider;
        private readonly ResolveAndExportStacksBackgroundLoop _resolveAndExportStacksBackgroundLoop;
        private readonly PProfBuilder _pprofBuilder;
        private readonly ProfilerEngineVersionInfo _versionInfo;
        private StackSnapshotsBufferSegmentCollection _completedStackSnapshots;

        private ProfilerEngine(IProductConfiguration config)
        {
            Validate.NotNull(config, nameof(config));

            _versionInfo = ProfilerEngineVersionInfo.CreateNewInstance();

            _profiledThreadInfoProvider = new ProfiledThreadInfoProvider();
            _profiledAppDomainProvider = new ProfiledAppDomainProvider();

            _enqueueStackSnapshotBufferSegmentForExport = NativeCallback_EnqueueStackSnapshotBufferSegmentForExport;
            _tryShutdownCurrentManagedProfilerEngine = NativeCallback_TryShutdownCurrentManagedProfilerEngine;

            _completedStackSnapshots = new StackSnapshotsBufferSegmentCollection();

            _pprofBuilder = new PProfBuilder();

            RegisterReversePInvokeCallbacks();

            _resolveAndExportStacksBackgroundLoop = new ResolveAndExportStacksBackgroundLoop(this, config);
            _resolveAndExportStacksBackgroundLoop.Start();
        }

        public static ProfilerEngine Current
        {
            get { return _currentEngine; }
        }

        internal ProfilerEngineVersionInfo VersionInfo
        {
            get { return _versionInfo; }
        }

        public static bool TryCreateAndStart(out ProfilerEngine runningProfilerEngine)
        {
            IProductConfiguration config = ProductConfigurationProvider.CreateDefault()
                                                                       // .ApplyReleaseDefaults()
                                                                       // .ApplyDevDefaults()
                                                                       .ApplyReleaseOrDevDefaults()
                                                                       .ApplyEnvironmentVariables()
                                                                       .CreateImmutableSnapshot();

            return TryCreateAndStart(config, out runningProfilerEngine);
        }

        public static bool TryCreateAndStart(IProductConfiguration config, out ProfilerEngine runningProfilerEngine)
        {
            Validate.NotNull(config, nameof(config));

            runningProfilerEngine = _currentEngine;
            if (runningProfilerEngine != null)
            {
                return false;
            }

            lock (_singletonAccessLock)
            {
                runningProfilerEngine = _currentEngine;
                if (runningProfilerEngine != null)
                {
                    return false;
                }

                CreateAndStart(config, out runningProfilerEngine);
                return (runningProfilerEngine != null);
            }
        }

        public void Dispose()
        {
            lock (_singletonAccessLock)
            {
                _profiledThreadInfoProvider.Dispose();
                _profiledAppDomainProvider.Dispose();

                UnregisterReversePInvokeCallbacks();

                _resolveAndExportStacksBackgroundLoop.Dispose();

                _pprofBuilder.Dispose();

                StackSnapshotsBufferSegmentCollection completedStackSnapshots = GetCompletedStackSnapshots();
                if (completedStackSnapshots != null)
                {
                    _completedStackSnapshots = null;
                    completedStackSnapshots.Dispose();
                }

                if (_currentEngine == this)
                {
                    _currentEngine = null;
                }
            }
        }

        internal ProfiledThreadInfoProvider GetProfiledThreadInfoProvider()
        {
            return _profiledThreadInfoProvider;
        }

        internal ProfiledAppDomainProvider GetProfiledAppDomainProvider()
        {
            return _profiledAppDomainProvider;
        }

        internal StackSnapshotsBufferSegmentCollection GetCompletedStackSnapshots()
        {
            StackSnapshotsBufferSegmentCollection completedStackSnapshots = Volatile.Read(ref _completedStackSnapshots);
            return completedStackSnapshots;
        }

        internal StackSnapshotsBufferSegmentCollection GetResetCompletedStackSnapshots()
        {
            var newCompletedStackSnapshots = new StackSnapshotsBufferSegmentCollection();

            StackSnapshotsBufferSegmentCollection prevCompletedStackSnapshots = Interlocked.Exchange(ref _completedStackSnapshots, newCompletedStackSnapshots);
            prevCompletedStackSnapshots.MakeReadonly();
            return prevCompletedStackSnapshots;
        }

        internal PProfBuilder GetPProfBuilder()
        {
            return _pprofBuilder;
        }

        /// <summary>
        /// Called internally AFTER <c>s_singletonAccessLock</c> has been taken and it was validated that no other engine is running.
        /// </summary>
        private static void CreateAndStart(IProductConfiguration config, out ProfilerEngine newProfilerEngine)
        {
            LogConfigurator.SetupLogger(config);
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "Initializing. Will create and start the managed profiler engine.");

            try
            {
                ThreadUtil.EnsureSetCurrentManagedThreadNameNativeCallbackInitialized();

                _currentEngine = new ProfilerEngine(config);
                newProfilerEngine = _currentEngine;

                LogInitializationCompleted(newProfilerEngine);
            }
            catch (Exception ex)
            {
                Log.Error(Log.WithCallInfo(LogSourceMoniker), "Initialization error.", ex);
                newProfilerEngine = null;
            }
        }

        private static void LogInitializationCompleted(ProfilerEngine initializedEngine)
        {
            try
            {
                AppDomain currentAppDomain = AppDomain.CurrentDomain;
                Thread currentThread = Thread.CurrentThread;
#pragma warning disable CS0618 // GetCurrentThreadId is obsolete but we can still use it for logging purposes (see respective docs)
                int osThreadId = AppDomain.GetCurrentThreadId();
#pragma warning restore CS0618 // Type or member is obsolete

                Log.Info(
                    Log.WithCallInfo(LogSourceMoniker),
                    $"Initialization completed. {nameof(ProfilerEngine)} is running.",
#pragma warning disable SA1117 // easier to have the mapping key/value on the same line
                    $"{nameof(ProfilerEngine)}.Type", typeof(ProfilerEngine).FullName,
                    $"{nameof(ProfilerEngine)}.{nameof(ProfilerEngineVersionInfo.BuildConfigurationMoniker)}", initializedEngine.VersionInfo.BuildConfigurationMoniker,
                    $"{nameof(ProfilerEngine)}.{nameof(ProfilerEngineVersionInfo.FileVersion)}", initializedEngine.VersionInfo.FileVersion,
                    $"{nameof(ProfilerEngine)}.{nameof(ProfilerEngineVersionInfo.InformationalVersion)}", initializedEngine.VersionInfo.InformationalVersion,
                    $"{nameof(ProfilerEngine)}.{nameof(ProfilerEngineVersionInfo.AssemblyName)}", initializedEngine.VersionInfo.AssemblyName,
                    $"{nameof(RuntimeEnvironmentInfo)}.{nameof(RuntimeEnvironmentInfo.RuntimeName)}", RuntimeEnvironmentInfo.SingeltonInstance.RuntimeName,
                    $"{nameof(RuntimeEnvironmentInfo)}.{nameof(RuntimeEnvironmentInfo.RuntimeVersion)}", RuntimeEnvironmentInfo.SingeltonInstance.RuntimeVersion,
                    $"{nameof(RuntimeEnvironmentInfo)}.{nameof(RuntimeEnvironmentInfo.ProcessArchitecture)}", RuntimeEnvironmentInfo.SingeltonInstance.ProcessArchitecture,
                    $"{nameof(RuntimeEnvironmentInfo)}.{nameof(RuntimeEnvironmentInfo.OsPlatform)}", RuntimeEnvironmentInfo.SingeltonInstance.OsPlatform,
                    $"{nameof(RuntimeEnvironmentInfo)}.{nameof(RuntimeEnvironmentInfo.OsArchitecture)}", RuntimeEnvironmentInfo.SingeltonInstance.OsArchitecture,
                    $"{nameof(RuntimeEnvironmentInfo)}.{nameof(RuntimeEnvironmentInfo.OsDescription)}", RuntimeEnvironmentInfo.SingeltonInstance.OsDescription,
                    $"{nameof(RuntimeEnvironmentInfo)}.{nameof(RuntimeEnvironmentInfo.CoreAssembyInfo.IsMscorlib)}", RuntimeEnvironmentInfo.SingeltonInstance.CoreAssembyInfo.IsMscorlib,
                    $"{nameof(RuntimeEnvironmentInfo)}.{nameof(RuntimeEnvironmentInfo.CoreAssembyInfo.IsSysPrivCoreLib)}", RuntimeEnvironmentInfo.SingeltonInstance.CoreAssembyInfo.IsSysPrivCoreLib,
                    $"{nameof(RuntimeEnvironmentInfo)}.{nameof(RuntimeEnvironmentInfo.CoreAssembyInfo.Name)}", RuntimeEnvironmentInfo.SingeltonInstance.CoreAssembyInfo.Name,
                    $"{nameof(currentThread)}.{nameof(Thread.Name)}", currentThread?.Name,
                    $"{nameof(currentThread)}.{nameof(Thread.ManagedThreadId)}", currentThread?.ManagedThreadId,
                    $"{nameof(currentThread)}.<{nameof(osThreadId)}>", osThreadId,
                    $"{nameof(currentThread)}.{nameof(Thread.IsThreadPoolThread)}", currentThread?.IsThreadPoolThread,
                    $"{nameof(currentThread)}.{nameof(Thread.IsBackground)}", currentThread?.IsBackground,
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.Id)}", currentAppDomain?.Id,
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.FriendlyName)}", currentAppDomain?.FriendlyName,
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.IsDefaultAppDomain)}", currentAppDomain?.IsDefaultAppDomain(),
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.BaseDirectory)}", currentAppDomain?.BaseDirectory,
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.DynamicDirectory)}", currentAppDomain?.DynamicDirectory,
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.IsFullyTrusted)}", currentAppDomain?.IsFullyTrusted,
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.IsHomogenous)}", currentAppDomain?.IsHomogenous,
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.RelativeSearchPath)}", currentAppDomain?.RelativeSearchPath,
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.ShadowCopyFiles)}", currentAppDomain?.ShadowCopyFiles);
#pragma warning restore SA1117 // Parameters should be on same line or separate lines
            }
            catch (Exception ex)
            {
                // This probably never happens in practice. If it ever actually occurs, need to investigate and restructure the Log accordingly.
                Log.Error(
                    Log.WithCallInfo(LogSourceMoniker),
                    $"Initialization completed. {nameof(ProfilerEngine)} is running. However, an error occurred while trying to log that very fact.",
                    ex);
            }
        }

        private void RegisterReversePInvokeCallbacks()
        {
            const string ErrorMessage = "While initializing, the Managed Profiler Engine overwrote an existing reverse PInvoke callback."
                                      + " This may indicate a serious problem with code or configuration."
                                      + " Perhaps, there are more than a single process-wide instance of " + nameof(ProfilerEngine) + "?"
                                      + " This could happen, for instance, if the Engine is being loaded into more than a single AppDomain."
                                      + " If so, it is not a supported scenario.";
            {
                NativeInterop.ManagedCallbackRegistry.EnqueueStackSnapshotBufferSegmentForExport.Delegate_t prevExistingCallback =
                    NativeInterop.ManagedCallbackRegistry.EnqueueStackSnapshotBufferSegmentForExport.Set(_enqueueStackSnapshotBufferSegmentForExport);

                if (prevExistingCallback != null)
                {
                    AppDomain currentAppDomain = AppDomain.CurrentDomain;

#pragma warning disable SA1117 // easier to have the mapping key/value on the same line
                    Log.Error(
                        Log.WithCallInfo(LogSourceMoniker),
                        ErrorMessage,
                        "Callback moniker", nameof(NativeInterop.ManagedCallbackRegistry.EnqueueStackSnapshotBufferSegmentForExport),
                        $"{nameof(currentAppDomain)}.{nameof(AppDomain.Id)}", currentAppDomain?.Id,
                        $"{nameof(currentAppDomain)}.{nameof(AppDomain.FriendlyName)}", currentAppDomain?.FriendlyName,
                        $"{nameof(currentAppDomain)}.{nameof(AppDomain.IsDefaultAppDomain)}", currentAppDomain?.IsDefaultAppDomain());
                }
            }

            {
                NativeInterop.ManagedCallbackRegistry.TryShutdownCurrentManagedProfilerEngine.Delegate_t prevExistingCallback =
                            NativeInterop.ManagedCallbackRegistry.TryShutdownCurrentManagedProfilerEngine.Set(_tryShutdownCurrentManagedProfilerEngine);

                if (prevExistingCallback != null)
                {
                    AppDomain currentAppDomain = AppDomain.CurrentDomain;

                    Log.Error(
                        Log.WithCallInfo(LogSourceMoniker),
                        ErrorMessage,
                        "Callback moniker", nameof(NativeInterop.ManagedCallbackRegistry.TryShutdownCurrentManagedProfilerEngine),
                        $"{nameof(currentAppDomain)}.{nameof(AppDomain.Id)}", currentAppDomain?.Id,
                        $"{nameof(currentAppDomain)}.{nameof(AppDomain.FriendlyName)}", currentAppDomain?.FriendlyName,
                        $"{nameof(currentAppDomain)}.{nameof(AppDomain.IsDefaultAppDomain)}", currentAppDomain?.IsDefaultAppDomain());
                }
#pragma warning restore SA1117 // Parameters should be on same line or separate lines
            }
        }

        private void UnregisterReversePInvokeCallbacks()
        {
            const string ErrorMessage = "While disposing, the Managed Profiler Engine erased a reverse PInvoke callback"
                                      + " that does not appear to refer to the engine instance being disposed."
                                      + " This may indicate a serious problem with code or configuration."
                                      + " Perhaps, there are more than a single process-wide instance of " + nameof(ProfilerEngine) + "?"
                                      + " This could happen, for instance, if the Engine was loaded into more than a single AppDomain."
                                      + " If so, it is not a supported scenario.";

            AppDomain currentAppDomain = null;

            var existingEnqueueCallback = NativeInterop.ManagedCallbackRegistry.EnqueueStackSnapshotBufferSegmentForExport.Set(null);
            if (existingEnqueueCallback != null && existingEnqueueCallback != _enqueueStackSnapshotBufferSegmentForExport)
            {
                currentAppDomain = AppDomain.CurrentDomain;

#pragma warning disable SA1117 // easier to have the mapping key/value on the same line
                Log.Error(
                    Log.WithCallInfo(LogSourceMoniker),
                    ErrorMessage,
                    "Callback moniker", nameof(NativeInterop.ManagedCallbackRegistry.EnqueueStackSnapshotBufferSegmentForExport),
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.Id)}", currentAppDomain?.Id,
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.FriendlyName)}", currentAppDomain?.FriendlyName,
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.IsDefaultAppDomain)}", currentAppDomain?.IsDefaultAppDomain());
            }

            var prevTryShutdownCallback = NativeInterop.ManagedCallbackRegistry.TryShutdownCurrentManagedProfilerEngine.Set(null);
            if (prevTryShutdownCallback != null && prevTryShutdownCallback != _tryShutdownCurrentManagedProfilerEngine)
            {
                currentAppDomain = AppDomain.CurrentDomain;

                Log.Error(
                    Log.WithCallInfo(LogSourceMoniker),
                    ErrorMessage,
                    "Callback moniker", nameof(NativeInterop.ManagedCallbackRegistry.TryShutdownCurrentManagedProfilerEngine),
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.Id)}", currentAppDomain?.Id,
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.FriendlyName)}", currentAppDomain?.FriendlyName,
                    $"{nameof(currentAppDomain)}.{nameof(AppDomain.IsDefaultAppDomain)}", currentAppDomain?.IsDefaultAppDomain());
            }
#pragma warning restore SA1117 // Parameters should be on same line or separate lines
        }

        private uint NativeCallback_EnqueueStackSnapshotBufferSegmentForExport(
                        IntPtr segmentNativeObjectPtr,
                        IntPtr segmentStartAddress,
                        uint segmentByteCount,
                        uint segmentSnapshotCount,
                        ulong segmentUnixTimeUtcRangeStart,
                        ulong segmentUnixTimeUtcRangeEnd)
        {
            StackSnapshotsBufferSegment segment = null;

            try
            {
                segment = new StackSnapshotsBufferSegment(
                                segmentNativeObjectPtr,
                                segmentStartAddress,
                                segmentByteCount,
                                segmentSnapshotCount,
                                segmentUnixTimeUtcRangeStart,
                                segmentUnixTimeUtcRangeEnd);

                StackSnapshotsBufferSegmentCollection completedStackSnapshots = GetCompletedStackSnapshots();
                bool isAdded = completedStackSnapshots != null && completedStackSnapshots.Add(segment);
                while (!isAdded)
                {
                    // If we get null, we are in a race on shutdown. There is nothing we can do, except just give up.
                    // We need will dispose the segment without calling native Release
                    // and return a failure code to indicate that we never kept a pointer in the first place.
                    if (completedStackSnapshots == null)
                    {
                        segment.DisposeWithoutNativeRelease();
                        segment = null;
                        return HResult.E_CHANGED_STATE;
                    }

                    Thread.Yield();
                    completedStackSnapshots = GetCompletedStackSnapshots();
                    isAdded = completedStackSnapshots != null && completedStackSnapshots.Add(segment);
                }
            }
            catch (Exception ex)
            {
                Log.Error(LogSourceMoniker, ex);

                if (segment != null)
                {
                    segment.DisposeWithoutNativeRelease();
                }

                return HResult.GetFailureCode(ex);
            }

            return HResult.S_OK;
        }

        private bool NativeCallback_TryShutdownCurrentManagedProfilerEngine()
        {
            this.Dispose();
            return true;
        }
    }
}