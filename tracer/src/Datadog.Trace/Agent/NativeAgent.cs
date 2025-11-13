// <copyright file="NativeAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent;

#pragma warning disable SA1602
#pragma warning disable SA1600
#pragma warning disable SA1201
#pragma warning disable SA1623
#pragma warning disable SA1503
#pragma warning disable SA1307
#pragma warning disable SA1310
#pragma warning disable SA1300

internal class NativeAgent : IDisposable
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NativeAgent));
    private static readonly Lazy<NativeAgent?> LazyInstance = new(Create);
    private IntPtr _agentHandle;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeAgent"/> class.
    /// </summary>
    /// <param name="config">Configuration options for the agent</param>
    /// <exception cref="DatadogAgentException">Thrown if agent initialization fails</exception>
    public NativeAgent(DatadogAgentConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        // Convert managed config to native options
        var nativeOptions = config.ToNativeOptions();

        try
        {
            var result = PInvokes.datadog_agent_start(ref nativeOptions);

            // Check for errors
            if (result.Error != DatadogError.Ok || result.Agent == IntPtr.Zero)
            {
                throw new DatadogAgentException($"Failed to start Datadog agent: {result.Error}. Check API key and configuration.");
            }

            _agentHandle = result.Agent;

            // Extract version string
            Version = Marshal.PtrToStringAnsi(result.Version) ?? "unknown";

            // Extract bound port
            BoundPort = result.BoundPort > 0 ? result.BoundPort : null;

            // Extract DogStatsD port
            DogStatsDPort = result.DogStatsdPort > 0 ? result.DogStatsdPort : null;

            // Extract UDS path
            UdsPath = result.UdsPath != IntPtr.Zero ? Marshal.PtrToStringAnsi(result.UdsPath) : null;
        }
        finally
        {
            // Free allocated native strings
            nativeOptions.FreeAllocatedStrings();
        }
    }

    public static NativeAgent Instance => LazyInstance.Value ?? throw new InvalidOperationException($"{nameof(NativeAgent)} is not initialized");

    /// <summary>
    /// Version of the native library
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Bound port of the HTTP server (-1 if not available)
    /// </summary>
    public int? BoundPort { get; }

    /// <summary>
    /// Actual bound port of the DogStatsD UDP server (-1 if not available)
    /// When dogstatsd_port was 0 (ephemeral), this contains the OS-assigned port
    /// </summary>
    public int? DogStatsDPort { get; }

    /// <summary>
    /// Unix Domain Socket path (null if not using UDS mode)
    /// Available when OperationalMode is HttpUds
    /// </summary>
    public string? UdsPath { get; }

    private static NativeAgent? Create()
    {
        try
        {
            var nAgent = new NativeAgent(
                new NativeAgent.DatadogAgentConfig
                {
                    AppSecEnabled = true,
                    LogLevel = NativeAgent.LogLevel.Debug,
                    DogStatsDEnabled = true
                });
            LifetimeManager.Instance.AddShutdownTask(_ =>
            {
                Log.Warning("Disposing Native Agent");
                nAgent.Dispose();
            });

            Log.Warning("Native Agent Version: {Version}", nAgent.Version);
            Log.Warning("Native Trace Agent Port: {Port}", nAgent.BoundPort);
            Log.Warning("Native UDP Port: {Port}", nAgent.DogStatsDPort);
            if (nAgent.UdsPath != null)
            {
                Log.Warning("Native UDS Path: {Path}", nAgent.UdsPath);
            }

            return nAgent;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Native Agent Error");
            return null;
        }
    }

    /// <summary>
    /// Waits for a shutdown signal (blocking).
    /// Returns when Ctrl+C, SIGTERM, or programmatic shutdown is triggered.
    /// </summary>
    /// <returns>Shutdown reason code</returns>
    public ShutdownReason WaitForShutdown()
    {
        ThrowIfDisposed();

        var reason = PInvokes.datadog_agent_wait_for_shutdown(_agentHandle);
        return (ShutdownReason)reason;
    }

    /// <summary>
    /// Stops the agent and frees all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_agentHandle != IntPtr.Zero)
        {
            PInvokes.datadog_agent_stop(_agentHandle);
            _agentHandle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~NativeAgent()
    {
        Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NativeAgent));
    }

    private static class PInvokes
    {
        // Platform-specific library names
        private const string LibraryName = "datadog_agent_native";

        /// <summary>
        /// Result returned from datadog_agent_start
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct DatadogAgentStartResult
        {
            public IntPtr Agent;
            public DatadogError Error;
            public IntPtr Version;
            public int BoundPort;
            public int DogStatsdPort;
            public IntPtr UdsPath;
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern DatadogAgentStartResult datadog_agent_start(ref DatadogAgentOptions options);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern DatadogError datadog_agent_stop(IntPtr agent);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int datadog_agent_wait_for_shutdown(IntPtr agent);
    }

    /// <summary>
    /// Configuration for the Datadog Agent.
    /// </summary>
    public class DatadogAgentConfig
    {
        /// <summary>
        /// Datadog API key (required).
        /// If null/empty, will be read from DD_API_KEY environment variable
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Datadog site (e.g., "datadoghq.com", "datadoghq.eu").
        /// If null/empty, will be read from DD_SITE environment variable, or default to "datadoghq.com"
        /// </summary>
        public string? Site { get; set; }

        /// <summary>
        /// Service name for Unified Service Tagging.
        /// If null/empty, will be read from DD_SERVICE environment variable
        /// </summary>
        public string? Service { get; set; }

        /// <summary>
        /// Environment name for Unified Service Tagging (e.g., "production", "staging").
        /// If null/empty, will be read from DD_ENV environment variable
        /// </summary>
        public string? Environment { get; set; }

        /// <summary>
        /// Version for Unified Service Tagging.
        /// If null/empty, will be read from DD_VERSION environment variable
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Enable Application Security (AppSec/WAF).
        /// Default: false
        /// </summary>
        public bool AppSecEnabled { get; set; } = false;

        /// <summary>
        /// Enable Remote Configuration.
        /// Default: true
        /// </summary>
        public bool RemoteConfigEnabled { get; set; } = true;

        /// <summary>
        /// Log level for the agent.
        /// Default: Info
        /// </summary>
        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// Operational mode for the agent.
        /// Default: HttpEphemeralPort (automatic port assignment)
        /// </summary>
        public OperationalMode OperationalMode { get; set; } = OperationalMode.HttpEphemeralPort;

        /// <summary>
        /// Trace agent port.
        /// null = default (8126)
        /// 0 = ephemeral (OS-assigned, recommended to avoid port conflicts)
        /// Positive number = specific port
        ///
        /// Note: Ignored if OperationalMode is HttpEphemeralPort or HttpUds.
        /// </summary>
        public int? TraceAgentPort { get; set; } = 0; // Ephemeral by default

        /// <summary>
        /// Enable DogStatsD UDP server for receiving metrics.
        /// Default: false (disabled)
        /// </summary>
        public bool DogStatsDEnabled { get; set; } = false;

        /// <summary>
        /// DogStatsD UDP port.
        /// null = default (8125)
        /// 0 = ephemeral (OS-assigned, recommended to avoid port conflicts)
        /// Positive number = specific port
        ///
        /// Note: Only used when DogStatsDEnabled is true
        /// </summary>
        public int? DogStatsDPort { get; set; } = 0; // Ephemeral by default

        /// <summary>
        /// Unix Domain Socket file permissions (Unix only).
        /// null = use default (0o600 / 384 decimal, owner read/write only - most secure)
        ///
        /// Common values:
        /// - 384 (0o600): owner read/write only (default, most secure)
        /// - 432 (0o660): owner + group read/write
        /// - 438 (0o666): all users read/write (not recommended)
        ///
        /// Note: Only used when OperationalMode is HttpUds
        /// </summary>
        public uint? TraceAgentUdsPermissions { get; set; } = null;

        internal DatadogAgentOptions ToNativeOptions()
        {
            return new DatadogAgentOptions
            {
                api_key = string.IsNullOrEmpty(ApiKey) ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(ApiKey),
                site = string.IsNullOrEmpty(Site) ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(Site),
                service = string.IsNullOrEmpty(Service) ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(Service),
                env = string.IsNullOrEmpty(Environment) ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(Environment),
                version = string.IsNullOrEmpty(Version) ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(Version),
                appsec_enabled = AppSecEnabled ? 1 : 0,
                remote_config_enabled = RemoteConfigEnabled ? 1 : 0,
                log_level = (int)LogLevel,
                operational_mode = (int)OperationalMode,
                trace_agent_port = TraceAgentPort ?? -1,  // null = -1 (use default 8126)
                dogstatsd_enabled = DogStatsDEnabled ? 1 : 0,
                dogstatsd_port = DogStatsDPort ?? -1,  // null = -1 (use default 8125)
                trace_agent_uds_permissions = TraceAgentUdsPermissions.HasValue ? (int)TraceAgentUdsPermissions.Value : -1
            };
        }
    }

    /// <summary>
    /// Native FFI options structure (matches Rust struct).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DatadogAgentOptions
    {
        public IntPtr api_key;
        public IntPtr site;
        public IntPtr service;
        public IntPtr env;
        public IntPtr version;
        public int appsec_enabled;
        public int remote_config_enabled;
        public int log_level;
        public int operational_mode;
        public int trace_agent_port;
        public int dogstatsd_enabled;
        public int dogstatsd_port;
        public int trace_agent_uds_permissions;

        internal void FreeAllocatedStrings()
        {
            if (api_key != IntPtr.Zero) Marshal.FreeHGlobal(api_key);
            if (site != IntPtr.Zero) Marshal.FreeHGlobal(site);
            if (service != IntPtr.Zero) Marshal.FreeHGlobal(service);
            if (env != IntPtr.Zero) Marshal.FreeHGlobal(env);
            if (version != IntPtr.Zero) Marshal.FreeHGlobal(version);
        }
    }

    /// <summary>
    /// Log levels for the agent.
    /// </summary>
    public enum LogLevel
    {
        Error = 0,
        Warn = 2,
        Info = 3,
        Debug = 4,
        Trace = 5
    }

    /// <summary>
    /// Operational modes for the agent.
    /// </summary>
    public enum OperationalMode
    {
        /// <summary>
        /// Traditional mode with fixed HTTP ports (default 8126).
        /// </summary>
        HttpFixedPort = 0,

        /// <summary>
        /// HTTP server with OS-assigned ports (port 0).
        /// Best for avoiding port conflicts.
        /// </summary>
        HttpEphemeralPort = 1,

        /// <summary>
        /// Unix Domain Socket mode (Unix only).
        /// Uses filesystem sockets for IPC with automatic PID-based paths.
        /// Most secure for single-process scenarios.
        /// </summary>
        HttpUds = 2
    }

    /// <summary>
    /// Shutdown reasons.
    /// </summary>
    public enum ShutdownReason
    {
        GracefulShutdown = 0,
        UserInterrupt = 1,
        FatalError = 2,
        Timeout = 3
    }

    /// <summary>
    /// Error codes from native FFI.
    /// </summary>
    public enum DatadogError
    {
        Ok = 0,
        NullPointer = 1,
        InvalidString = 2,
        ConfigError = 3,
        InitError = 4,
        StartupError = 5,
        ShutdownError = 6,
        RuntimeError = 7,
        InvalidDataFormat = 8,
        SubmissionError = 9,
        NotAvailable = 10,
        UnknownError = 99
    }

    /// <summary>
    /// Exception thrown by Datadog Agent operations.
    /// </summary>
    public class DatadogAgentException : Exception
    {
        public DatadogAgentException(string message)
            : base(message)
        {
        }

        public DatadogAgentException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
