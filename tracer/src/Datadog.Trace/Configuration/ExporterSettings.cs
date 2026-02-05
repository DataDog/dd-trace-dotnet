// <copyright file="ExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.StatsdClient.Transport;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains exporter settings.
    /// </summary>
    public sealed partial class ExporterSettings
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExporterSettings));

        /// <summary>
        /// Allows overriding of file system access for tests.
        /// </summary>
        private readonly Func<string, bool> _fileExists;
        private readonly IConfigurationTelemetry _telemetry;

        /// <summary>
        /// The default port value for dogstatsd when sending over UDP.
        /// </summary>
        internal const int DefaultDogstatsdPort = 8125;

        /// <summary>
        /// The default port value for dogstatsd when sending over UDP.
        /// </summary>
        internal const string DefaultDogstatsdHostname = "127.0.0.1";

        /// <summary>
        /// Default metrics UDS path.
        /// </summary>
        internal const string DefaultMetricsUnixDomainSocket = "/var/run/datadog/dsd.socket";
        internal const string UdpPrefix = "udp://";

        [TestingOnly]
        internal ExporterSettings()
            : this(source: null, new ConfigurationTelemetry())
        {
        }

        [TestingOnly]
        internal ExporterSettings(IConfigurationSource? source)
            : this(source, File.Exists, new ConfigurationTelemetry())
        {
        }

        internal ExporterSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
            : this(source, File.Exists, telemetry)
        {
        }

        internal ExporterSettings(Raw rawSettings, IConfigurationTelemetry telemetry)
            : this(rawSettings, File.Exists, telemetry)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExporterSettings"/> class.
        /// Direct use in tests only.
        /// </summary>
        internal ExporterSettings(IConfigurationSource? source, Func<string, bool> fileExists, IConfigurationTelemetry telemetry)
            : this(new Raw(source ?? NullConfigurationSource.Instance, telemetry), fileExists, telemetry)
        {
        }

        internal ExporterSettings(Raw rawSettings, Func<string, bool> fileExists, IConfigurationTelemetry telemetry)
        {
            _fileExists = fileExists;
            _telemetry = telemetry;
            RawSettings = rawSettings;

            ValidationWarnings = new List<string>();

            // Generate unique pipe names for Azure Functions (when not using AAS Site Extension) if not explicitly configured
            // This ensures multi-function scenarios don't conflict when using the compat layer
            // If names are explicitly configured, use them as-is
            var tracesPipeName = rawSettings.TracesPipeName;
            var metricsPipeName = rawSettings.MetricsPipeName;

            if (Util.EnvironmentHelpers.IsAzureFunctions() && !Util.EnvironmentHelpers.IsUsingAzureAppServicesSiteExtension())
            {
                if (string.IsNullOrEmpty(tracesPipeName))
                {
                    tracesPipeName = GenerateUniquePipeName("dd_trace");
                    AzureFunctionsGeneratedTracesPipeName = tracesPipeName;
                    Log.Information("Azure Functions environment detected with no explicit trace pipe name. Generated unique pipe name: {TracesPipeName}", tracesPipeName);
                }

                if (string.IsNullOrEmpty(metricsPipeName))
                {
                    metricsPipeName = GenerateUniquePipeName("dd_dogstatsd");
                    AzureFunctionsGeneratedMetricsPipeName = metricsPipeName;
                    Log.Information("Azure Functions environment detected with no explicit metrics pipe name. Generated unique pipe name: {MetricsPipeName}", metricsPipeName);
                }
            }

            var traceSettings = GetTraceTransport(
                agentUri: rawSettings.TraceAgentUri,
                tracesPipeName: tracesPipeName,
                agentHost: rawSettings.TraceAgentHost,
                agentPort: rawSettings.TraceAgentPort,
                tracesUnixDomainSocketPath: rawSettings.TracesUnixDomainSocketPath);

            TracesTransport = traceSettings.Transport;
            TracesPipeName = traceSettings.PipeName;
            TracesUnixDomainSocketPath = traceSettings.UdsPath;
            AgentUri = traceSettings.AgentUri;

            var metricsSettings = ConfigureMetricsTransport(
                metricsUrl: rawSettings.MetricsUrl,
                traceAgentUrl: rawSettings.TraceAgentUri,
                agentHost: rawSettings.TraceAgentHost,
                dogStatsdPort: rawSettings.DogStatsdPort,
                metricsPipeName: metricsPipeName,
                metricsUnixDomainSocketPath: rawSettings.MetricsUnixDomainSocketPath);

            MetricsHostname = metricsSettings.Hostname;
            MetricsUnixDomainSocketPath = metricsSettings.UdsPath;
            MetricsTransport = metricsSettings.Transport;
            MetricsPipeName = metricsSettings.PipeName;
            DogStatsdPort = metricsSettings.DogStatsdPort > 0
                                ? metricsSettings.DogStatsdPort
                                : (rawSettings.DogStatsdPort > 0 ? rawSettings.DogStatsdPort : DefaultDogstatsdPort);

            TracesPipeTimeoutMs = rawSettings.TracesPipeTimeoutMs;
        }

        /// <summary>
        /// Gets the generated trace pipe name for Azure Functions coordination.
        /// This is set when running in Azure Functions without explicit pipe name configuration.
        /// Used by ServerlessCompat instrumentation to coordinate pipe names with compat layer.
        /// </summary>
        internal static string? AzureFunctionsGeneratedTracesPipeName { get; private set; }

        /// <summary>
        /// Gets the generated metrics pipe name for Azure Functions coordination.
        /// This is set when running in Azure Functions without explicit pipe name configuration.
        /// Used by ServerlessCompat instrumentation to coordinate pipe names with compat layer.
        /// </summary>
        internal static string? AzureFunctionsGeneratedMetricsPipeName { get; private set; }

        /// <summary>
        /// Gets the Uri where the Tracer can connect to the Agent.
        /// Default is <c>"http://localhost:8126"</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AgentUri"/>
        /// <seealso cref="ConfigurationKeys.AgentHost"/>
        /// <seealso cref="ConfigurationKeys.AgentPort"/>
        public Uri AgentUri { get; }

        /// <summary>
        /// Gets the base Uri where traces will be sent, taking the transport into account. It may be
        /// different for statsd.
        /// </summary>
        /// <seealso cref="AgentUri"/>
        public string TraceAgentUriBase => TracesTransport switch
        {
            // Only named pipes doesn't set this prefix on AgentUri, so we need to use the pipe name here
            // Ideally, we would likely prefer to just have AgentUri include this prefix, but
            // 1. That's not a valid Uri - we would need to use a custom Uri scheme (e.g. npipe://) and also
            //    use / instead of \, i.e. @$"npipe:////./pipe/{TracesPipeName}"
            // 2. AgentUri is exposed publicly, so we can't change it without potentially breaking behaviour
            TracesTransportType.WindowsNamedPipe => $@"\\.\pipe\{TracesPipeName}",
            _ => AgentUri.ToString()
        };

        /// <summary>
        /// Gets the windows pipe name where the Tracer can connect to the Agent.
        /// Default is <c>null</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesPipeName"/>
        public string? TracesPipeName { get; }

        /// <summary>
        /// Gets the timeout in milliseconds for the windows named pipe requests.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesPipeTimeoutMs"/>
        public int TracesPipeTimeoutMs { get; }

        /// <summary>
        /// Gets the windows pipe name where the Tracer can send stats.
        /// Default is <c>null</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsPipeName"/>
        public string? MetricsPipeName { get; }

        /// <summary>
        /// Gets the unix domain socket path where the Tracer can connect to the Agent.
        /// This parameter is deprecated and shall be removed. Consider using AgentUri instead
        /// </summary>
        public string? TracesUnixDomainSocketPath { get; }

        /// <summary>
        /// Gets the unix domain socket path where the Tracer can send stats.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsUnixDomainSocketPath"/>
        public string? MetricsUnixDomainSocketPath { get; }

        /// <summary>
        /// Gets the port where the DogStatsd server is listening for connections.
        /// Default is <c>8125</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DogStatsdPort"/>
        public int DogStatsdPort { get; }

        /// <summary>
        /// Gets the transport used to send traces to the Agent.
        /// </summary>
        internal TracesTransportType TracesTransport { get; }

        /// <summary>
        /// Gets the transport used to connect to the DogStatsD.
        /// Default is <c>TransportStrategy.Tcp</c>.
        /// </summary>
        internal MetricsTransportType MetricsTransport { get; }

        /// <summary>
        /// Gets the agent host to use when <see cref="MetricsTransport"/> is <see cref="TransportType.UDP"/>
        /// </summary>
        internal string MetricsHostname { get; }

        internal List<string> ValidationWarnings { get; }

        internal IConfigurationTelemetry Telemetry => _telemetry;

        internal Raw RawSettings { get; }

        [TestingOnly]
        internal static ExporterSettings Create(Dictionary<string, object?> settings)
            => new(new DictionaryConfigurationSource(settings.ToDictionary(x => x.Key, x => x.Value?.ToString()!)), new ConfigurationTelemetry());

        private static string GetMetricsHostNameFromAgentUri(Uri agentUri)
        {
            // If the customer has enabled UDS traces then the AgentUri will have
            // the UDS path set for it, and the DnsSafeHost returns "".
            var traceHostname = agentUri.DnsSafeHost;
            return string.IsNullOrEmpty(traceHostname) ? DefaultDogstatsdHostname : traceHostname;
        }

        /// <summary>
        /// Generates a unique pipe name by appending a GUID to the base name.
        /// Used in Azure Functions to avoid conflicts when multiple functions run in the same hosting plan.
        /// </summary>
        /// <param name="baseName">The base name for the pipe</param>
        /// <returns>A unique pipe name in the format {base}_{guid}</returns>
        private static string GenerateUniquePipeName(string baseName)
        {
            // Validate base pipe name length before appending GUID
            // Windows pipe path format: \\.\pipe\{base}_{guid}
            // Max total: 256 - 9 (\\.\pipe\) - 1 (underscore) - 32 (GUID) = 214
            const int maxBaseLength = 214;

            if (baseName.Length > maxBaseLength)
            {
                Log.Warning<int, int>("Pipe base name exceeds {MaxLength} characters ({ActualLength}). Truncating to allow for GUID suffix.", maxBaseLength, baseName.Length);
                baseName = baseName.Substring(0, maxBaseLength);
            }

            var guid = Guid.NewGuid().ToString("N"); // "N" format removes hyphens (32 chars)
            var uniqueName = $"{baseName}_{guid}";

            return uniqueName;
        }

        private MetricsTransportSettings ConfigureMetricsTransport(string? metricsUrl, string? traceAgentUrl, string? agentHost, int dogStatsdPort, string? metricsPipeName, string? metricsUnixDomainSocketPath)
        {
            if (!string.IsNullOrEmpty(metricsUrl) && TryGetMetricsUriAndTransport(metricsUrl!, out var settingsFromUri))
            {
                return settingsFromUri;
            }

            // Agent port is set to zero in places like AAS where it's needed to prevent port conflict
            // The agent will fail to start if it can not bind a port, so we need to override 8125 to prevent port conflict
            // Port 0 means it will pick some random available port
            if (dogStatsdPort < 0)
            {
                ValidationWarnings.Add("The provided dogStatsD port isn't valid, it should be positive.");
            }

            var dogStatsDPortSource = dogStatsdPort == 0 ? null : ConfigurationKeys.DogStatsdPort;
            var dogStatsdPortToUse = dogStatsdPort > 0 ? dogStatsdPort : DefaultDogstatsdPort;

            MetricsTransportSettings settings;

            if (!string.IsNullOrEmpty(traceAgentUrl)
             && !traceAgentUrl!.StartsWith(UnixDomainSocketPrefix)
             && Uri.TryCreate(traceAgentUrl, UriKind.Absolute, out var tcpUri))
            {
                SetUdp(
                    hostname: GetMetricsHostNameFromAgentUri(tcpUri),
                    hostnameSource: ConfigurationKeys.AgentUri,
                    port: dogStatsdPortToUse,
                    portSource: dogStatsDPortSource,
                    out settings);
            }
            else if (dogStatsdPort > 0 || agentHost != null)
            {
                var hostSource = agentHost is null ? ConfigurationKeys.AgentHost : null;
                SetUdp(
                    hostname: agentHost ?? DefaultDogstatsdHostname,
                    hostnameSource: hostSource,
                    port: dogStatsdPortToUse,
                    portSource: dogStatsDPortSource,
                    out settings);
            }
            else if (!string.IsNullOrEmpty(metricsPipeName))
            {
                settings = new MetricsTransportSettings(TransportType.NamedPipe, PipeName: metricsPipeName);
            }
            else if (!string.IsNullOrEmpty(metricsUnixDomainSocketPath))
            {
#if NETCOREAPP3_1_OR_GREATER
                SetUds(metricsUnixDomainSocketPath, metricsUnixDomainSocketPath, metricsUnixDomainSocketPath, ConfigurationKeys.MetricsUnixDomainSocketPath, out settings);
#else
                // .NET Core 2.1 and .NET FX don't support Unix Domain Sockets
                ValidationWarnings.Add($"Found metrics UDS configuration {metricsUnixDomainSocketPath}, but current runtime doesn't support UDS, so ignoring it.");
                _telemetry.Record(
                    ConfigurationKeys.MetricsUnixDomainSocketPath,
                    metricsUnixDomainSocketPath,
                    recordValue: true,
                    ConfigurationOrigins.Default,
                    TelemetryErrorCode.UdsOnUnsupportedPlatform);
                SetDefault(out settings);
#endif
            }
#if NETCOREAPP3_1_OR_GREATER
            // .NET Core 2.1 and .NET FX don't support Unix Domain Sockets, so we don't care if the file already exists
            else if (_fileExists(DefaultMetricsUnixDomainSocket))
            {
                SetUds(DefaultMetricsUnixDomainSocket, DefaultMetricsUnixDomainSocket, DefaultMetricsUnixDomainSocket, null, out settings);
            }
#endif
            else
            {
                SetDefault(out settings);
            }

            // set these values if they're not already set just to keep some things happy
            return settings;

            bool TryGetMetricsUriAndTransport(string metricsUrl, out MetricsTransportSettings settings)
            {
                if (!Uri.TryCreate(metricsUrl, UriKind.Absolute, out var uri))
                {
                    ValidationWarnings.Add($"The Uri: '{metricsUrl}' in {ConfigurationKeys.MetricsUri} is not valid. It won't be taken into account to send metrics. Note that only absolute urls are accepted.");
                    settings = default;
                    return false;
                }

                var origin = ConfigurationOrigins.Default; // only called from the constructor
                if (uri.OriginalString.StartsWith(UnixDomainSocketPrefix, StringComparison.OrdinalIgnoreCase))
                {
#if NETCOREAPP3_1_OR_GREATER
                    var absoluteUri = uri.AbsoluteUri.Replace(UnixDomainSocketPrefix, string.Empty);
                    var probablyValid = SetUds(uri.PathAndQuery, uri.OriginalString, absoluteUri, ConfigurationKeys.AgentUri, out settings);
                    _telemetry.Record(
                        ConfigurationKeys.MetricsUnixDomainSocketPath,
                        MetricsUnixDomainSocketPath,
                        recordValue: true,
                        origin,
                        probablyValid ? null : TelemetryErrorCode.PotentiallyInvalidUdsPath);
#else
                    // .NET Core 2.1 and .NET FX don't support Unix Domain Sockets, but it's _explicitly_ being
                    // configured here, so warn the user, and switch to using the default transport instead.
                    ValidationWarnings.Add($"The provided metrics Uri {uri} represents a Unix Domain Socket (UDS), but the current runtime doesn't support UDS. Falling back to the default UDP transport.");
                    _telemetry.Record(
                        ConfigurationKeys.MetricsUnixDomainSocketPath,
                        metricsUrl,
                        recordValue: true,
                        origin,
                        TelemetryErrorCode.UdsOnUnsupportedPlatform);
                    SetDefault(out settings);
#endif
                    return true;
                }

                if (uri.OriginalString.StartsWith(UdpPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // If you don't specify the port explicitly in the url, it uses -1
                    var port = uri.Port > 0 ? uri.Port : DefaultDogstatsdPort;

                    SetUdp(
                        hostname: uri.Host,
                        hostnameSource: ConfigurationKeys.AgentUri,
                        port: port,
                        portSource: ConfigurationKeys.AgentUri,
                        out settings);
                    return true;
                }

                ValidationWarnings.Add($"Unknown transport type in {ConfigurationKeys.MetricsUri}. Only {UdpPrefix} and {UnixDomainSocketPrefix} are supported. It won't be taken into account to send metrics.");
                settings = default;
                return false;
            }

#if NETCOREAPP3_1_OR_GREATER
            bool SetUds(string unixSocket, string original, string absoluteUri, string? source, out MetricsTransportSettings settings)
            {
                // Only called in the constructor;
                var probablyValid = true;
                if (source is not null && !Path.IsPathRooted(absoluteUri))
                {
                    probablyValid = false;
                    ValidationWarnings.Add($"The provided metrics Uri {original} contains a relative path which may not work. This is the path to the socket that will be used: {unixSocket}");
                }

                // check if the file exists to warn the user.
                if (source is not null && !_fileExists(unixSocket))
                {
                    // We don't fallback in that case as the file could be mounted separately.
                    probablyValid = false;
                    ValidationWarnings.Add($"The socket {unixSocket} provided in '{source}' cannot be found. The tracer will still rely on this socket to send metrics.");
                }

                // Use "default" values for unused settings - this could do with some heavy refactoring
                settings = new(MetricsTransportType.UDS, UdsPath: unixSocket);
                return probablyValid;
            }
#endif
            bool SetUdp(string hostname, string? hostnameSource, int port, string? portSource, out MetricsTransportSettings settings)
            {
                var probablyValid = true;
                string metricsHostname;

                if (string.Equals(hostname, "localhost", StringComparison.OrdinalIgnoreCase))
                {
                    // Replace localhost with 127.0.0.1 to avoid DNS resolution.
                    // When ipv6 is enabled, localhost is first resolved to ::1, which fails
                    // because the trace agent is only bound to ipv4.
                    // This causes delays when sending traces.
                    metricsHostname = "127.0.0.1";
                }
                else
                {
                    metricsHostname = hostname; // assumes that agentHost is a valid hostname or IP address, just warn if it's not
                    if (hostnameSource is not null && Uri.CheckHostName(hostname) is not (UriHostNameType.IPv4 or UriHostNameType.IPv6 or UriHostNameType.Dns))
                    {
                        probablyValid = false;
                        ValidationWarnings.Add($"The provided agent host '{hostname}' in '{hostnameSource}' is not a valid hostname or IP address.");
                    }
                }

                // Agent port is set to zero in places like AAS where it's needed to prevent port conflict
                // The agent will fail to start if it can not bind a port, so we need to override 8125 to prevent port conflict
                // Port 0 means it will pick some random available port
                if (portSource is not null && port < 0)
                {
                    probablyValid = false;
                    ValidationWarnings.Add(FormattableString.Invariant($"The provided dogStatsD port '{port}' from {portSource} isn't valid, it should be positive."));
                }

                settings = new(MetricsTransportType.UDP, Hostname: metricsHostname, DogStatsdPort: port);
                // TODO: use this to mark config as errors
                return probablyValid;
            }

            void SetDefault(out MetricsTransportSettings settings) => SetUdp(
                hostname: DefaultDogstatsdHostname,
                hostnameSource: null,
                port: DefaultDogstatsdPort,
                portSource: null,
                out settings);
        }

        private void RecordTraceTransport(string transport, ConfigurationOrigins origin = ConfigurationOrigins.Default)
            => _telemetry.Record(ConfigTelemetryData.AgentTraceTransport, transport, recordValue: true, origin);

        private readonly record struct MetricsTransportSettings(MetricsTransportType Transport, string Hostname = DefaultDogstatsdHostname, int DogStatsdPort = 0, string? UdsPath = null, string? PipeName = null);

        /// <summary>
        /// These contain the "raw" settings loaded from config. If these don't change, the exporter settings also won't change
        /// </summary>
        internal sealed record Raw
        {
            public Raw(IConfigurationSource source, IConfigurationTelemetry telemetry)
            {
                // Get values from the config
                var config = new ConfigurationBuilder(source, telemetry);
                // NOTE: Keep this in sync with CreateUpdatedFromManualConfig below
                TraceAgentUri = config.WithKeys(ConfigurationKeys.AgentUri).AsString()?.Trim();
                TracesPipeName = config.WithKeys(ConfigurationKeys.TracesPipeName).AsString()?.Trim();
                TracesUnixDomainSocketPath = config.WithKeys(ConfigurationKeys.TracesUnixDomainSocketPath).AsString()?.Trim();

                TraceAgentHost = config
                               .WithKeys(ConfigurationKeys.AgentHost)
                               .AsString()?.Trim();

                TraceAgentPort = config
                               .WithKeys(ConfigurationKeys.AgentPort)
                               .AsInt32();

                MetricsUrl = config.WithKeys(ConfigurationKeys.MetricsUri).AsString()?.Trim();
                DogStatsdPort = config.WithKeys(ConfigurationKeys.DogStatsdPort).AsInt32(0);
                MetricsPipeName = config.WithKeys(ConfigurationKeys.MetricsPipeName).AsString()?.Trim();
                MetricsUnixDomainSocketPath = config.WithKeys(ConfigurationKeys.MetricsUnixDomainSocketPath).AsString()?.Trim();

                TracesPipeTimeoutMs = config
                                     .WithKeys(ConfigurationKeys.TracesPipeTimeoutMs)
                                     .AsInt32(500, value => value > 0)
                                     .Value;
            }

            /// <summary>
            /// Gets the Uri where the Tracer can connect to the Agent.
            /// </summary>
            /// <seealso cref="ConfigurationKeys.AgentUri"/>
            public string? TraceAgentUri { get; private init; }

            /// <summary>
            /// Gets the host where the Tracer can connect to the Agent.
            /// </summary>
            /// <seealso cref="ConfigurationKeys.AgentUri"/>
            public string? TraceAgentHost { get; }

            /// <summary>
            /// Gets the port where the Tracer can connect to the Agent.
            /// </summary>
            /// <seealso cref="ConfigurationKeys.AgentUri"/>
            public int? TraceAgentPort { get; }

            /// <summary>
            /// Gets the windows pipe name where the Tracer can connect to the Agent.
            /// </summary>
            /// <seealso cref="ConfigurationKeys.TracesPipeName"/>
            public string? TracesPipeName { get; }

            /// <summary>
            /// Gets the timeout in milliseconds for the windows named pipe requests.
            /// Default is <c>100</c>.
            /// </summary>
            /// <seealso cref="ConfigurationKeys.TracesPipeTimeoutMs"/>
            public int TracesPipeTimeoutMs { get; }

            /// <summary>
            /// Gets the Uri where the Tracer can connect to statsd.
            /// </summary>
            /// <seealso cref="ConfigurationKeys.AgentUri"/>
            public string? MetricsUrl { get; }

            /// <summary>
            /// Gets the windows pipe name where the Tracer can send stats.
            /// Default is <c>null</c>.
            /// </summary>
            /// <seealso cref="ConfigurationKeys.MetricsPipeName"/>
            public string? MetricsPipeName { get; }

            /// <summary>
            /// Gets the unix domain socket path where the Tracer can connect to the Agent.
            /// This parameter is deprecated and shall be removed. Consider using AgentUri instead
            /// </summary>
            public string? TracesUnixDomainSocketPath { get; }

            /// <summary>
            /// Gets the unix domain socket path where the Tracer can send stats.
            /// </summary>
            /// <seealso cref="ConfigurationKeys.MetricsUnixDomainSocketPath"/>
            public string? MetricsUnixDomainSocketPath { get; }

            /// <summary>
            /// Gets the port where the DogStatsd server is listening for connections.
            /// </summary>
            public int DogStatsdPort { get; }

            /// <summary>
            /// Creates a duplicate <see cref="Raw"/> instance, reading settings from a manual instrumentation
            /// <see cref="IConfigurationSource"/>. Only settings which _can_ be changed in manual instrumentation
            /// will be updated.
            /// </summary>
            /// <param name="rawSettings">The settings to base the new settings on</param>
            /// <param name="manualConfig">The manual instrumentation configuration source</param>
            /// <param name="telemetry">A telemetry instance to collect the updated telemetry</param>
            /// <param name="useDefaultSources">If true, the current <see cref="Raw.TraceAgentUri"/> is used as a fallback,
            /// otherwise <c>null</c> is used</param>
            /// <returns>A new <see cref="Raw"/> instance.</returns>
            internal static Raw CreateUpdatedFromManualConfig(
                Raw rawSettings,
                ManualInstrumentationConfigurationSourceBase manualConfig,
                IConfigurationTelemetry telemetry,
                bool useDefaultSources)
            {
                var config = new ConfigurationBuilder(manualConfig, telemetry);

                var manualResult = config.WithKeys(ConfigurationKeys.AgentUri).AsStringResult();
                var fallbackAgentUri = useDefaultSources ? rawSettings.TraceAgentUri : null;
                var agentUri = GetResult(manualResult, telemetry, fallbackAgentUri);

                return rawSettings with { TraceAgentUri = agentUri };

                string? GetResult(
                    ConfigurationBuilder.ClassConfigurationResultWithKey<string> classConfigurationResultWithKey,
                    IConfigurationTelemetry configurationTelemetry,
                    string? fallback)
                {
                    if (classConfigurationResultWithKey.ConfigurationResult is { IsValid: true, Result: var r1 })
                    {
                        return r1;
                    }

                    // Have to "re-record" the value so telemetry has the correct value
                    // Ideally we would know the "real" source, but we don't
                    if (classConfigurationResultWithKey.ConfigurationResult.IsPresent)
                    {
                        configurationTelemetry.Record(classConfigurationResultWithKey.Key, fallback, recordValue: true, ConfigurationOrigins.Calculated);
                    }

                    return fallback;
                }
            }
        }
    }
}
