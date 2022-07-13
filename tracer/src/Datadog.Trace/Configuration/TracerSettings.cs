// <copyright file="TracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains Tracer settings.
    /// </summary>
    public class TracerSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values.
        /// </summary>
        public TracerSettings()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values,
        /// or initializes the configuration from environment variables and configuration files.
        /// Calling <c>new TracerSettings(true)</c> is equivalent to calling <c>TracerSettings.FromDefaultSources()</c>
        /// </summary>
        /// <param name="useDefaultSources">If <c>true</c>, creates a <see cref="TracerSettings"/> populated from
        /// the default sources such as environment variables etc. If <c>false</c>, uses the default values.</param>
        public TracerSettings(bool useDefaultSources)
            : this(useDefaultSources ? CreateDefaultConfigurationSource() : null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        public TracerSettings(IConfigurationSource source)
        {
            Environment = source?.GetString(ConfigurationKeys.Environment);

            ServiceName = source?.GetString(ConfigurationKeys.ServiceName) ??
                          // backwards compatibility for names used in the past
                          source?.GetString("DD_SERVICE_NAME");

            ServiceVersion = source?.GetString(ConfigurationKeys.ServiceVersion);

            TraceEnabled = source?.GetBool(ConfigurationKeys.TraceEnabled) ??
                           // default value
                           true;

            if (AzureAppServices.Metadata.IsRelevant && AzureAppServices.Metadata.IsUnsafeToTrace)
            {
                TraceEnabled = false;
            }

            var disabledIntegrationNames = source?.GetString(ConfigurationKeys.DisabledIntegrations)
                                                 ?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ??
                                           Enumerable.Empty<string>();

            DisabledIntegrationNames = new HashSet<string>(disabledIntegrationNames, StringComparer.OrdinalIgnoreCase);

            Integrations = new IntegrationSettingsCollection(source);

            Exporter = new ExporterSettings(source);

#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabled = source?.GetBool(ConfigurationKeys.GlobalAnalyticsEnabled) ??
                               // default value
                               false;
#pragma warning restore 618

            MaxTracesSubmittedPerSecond = source?.GetInt32(ConfigurationKeys.TraceRateLimit) ??
#pragma warning disable 618 // this parameter has been replaced but may still be used
                                          source?.GetInt32(ConfigurationKeys.MaxTracesSubmittedPerSecond) ??
#pragma warning restore 618
                                          // default value
                                          100;

            GlobalTags = source?.GetDictionary(ConfigurationKeys.GlobalTags) ??
                         // backwards compatibility for names used in the past
                         source?.GetDictionary("DD_TRACE_GLOBAL_TAGS") ??
                         // default value (empty)
                         new ConcurrentDictionary<string, string>();

            // Filter out tags with empty keys or empty values, and trim whitespace
            GlobalTags = GlobalTags.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                                   .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());

            var inputHeaderTags = source?.GetDictionary(ConfigurationKeys.HeaderTags, allowOptionalMappings: true) ??
                         // default value (empty)
                         new Dictionary<string, string>();

            var headerTagsNormalizationFixEnabled = source?.GetBool(ConfigurationKeys.FeatureFlags.HeaderTagsNormalizationFixEnabled) ?? true;
            // Filter out tags with empty keys or empty values, and trim whitespaces
            HeaderTags = InitializeHeaderTags(inputHeaderTags, headerTagsNormalizationFixEnabled);

            var serviceNameMappings = source?.GetDictionary(ConfigurationKeys.ServiceNameMappings)
                                      ?.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                                      ?.ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());

            ServiceNameMappings = new ServiceNames(serviceNameMappings);

            TracerMetricsEnabled = source?.GetBool(ConfigurationKeys.TracerMetricsEnabled) ??
                                   // default value
                                   false;

            StatsComputationEnabled = source?.GetBool(ConfigurationKeys.StatsComputationEnabled) ?? false;

            RuntimeMetricsEnabled = source?.GetBool(ConfigurationKeys.RuntimeMetricsEnabled) ??
                                    false;

            CustomSamplingRules = source?.GetString(ConfigurationKeys.CustomSamplingRules);

            GlobalSamplingRate = source?.GetDouble(ConfigurationKeys.GlobalSamplingRate);

            StartupDiagnosticLogEnabled = source?.GetBool(ConfigurationKeys.StartupDiagnosticLogEnabled) ??
                                          // default value
                                          true;

            var urlSubstringSkips = source?.GetString(ConfigurationKeys.HttpClientExcludedUrlSubstrings) ??
                                    // default value
                                    (AzureAppServices.Metadata.IsRelevant ? AzureAppServices.Metadata.DefaultHttpClientExclusions : null);

            if (urlSubstringSkips != null)
            {
                HttpClientExcludedUrlSubstrings = TrimSplitString(urlSubstringSkips.ToUpperInvariant(), ',').ToArray();
            }

            var httpServerErrorStatusCodes = source?.GetString(ConfigurationKeys.HttpServerErrorStatusCodes) ??
                                           // Default value
                                           "500-599";

            HttpServerErrorStatusCodes = ParseHttpCodesToArray(httpServerErrorStatusCodes);

            var httpClientErrorStatusCodes = source?.GetString(ConfigurationKeys.HttpClientErrorStatusCodes) ??
                                        // Default value
                                        "400-499";
            HttpClientErrorStatusCodes = ParseHttpCodesToArray(httpClientErrorStatusCodes);

            TraceBufferSize = source?.GetInt32(ConfigurationKeys.BufferSize)
                ?? 1024 * 1024 * 10; // 10MB

            TraceBatchInterval = source?.GetInt32(ConfigurationKeys.SerializationBatchInterval)
                        ?? 100;

            RouteTemplateResourceNamesEnabled = source?.GetBool(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled)
                                                   ?? true;

            ExpandRouteTemplatesEnabled = source?.GetBool(ConfigurationKeys.ExpandRouteTemplatesEnabled)
                                        // disabled by default if route template resource names enabled
                                        ?? !RouteTemplateResourceNamesEnabled;

            KafkaCreateConsumerScopeEnabled = source?.GetBool(ConfigurationKeys.KafkaCreateConsumerScopeEnabled)
                                           ?? true; // default

            DelayWcfInstrumentationEnabled = source?.GetBool(ConfigurationKeys.FeatureFlags.DelayWcfInstrumentationEnabled)
                                            ?? false;

            PropagationStyleInject = TrimSplitString(source?.GetString(ConfigurationKeys.PropagationStyleInject) ?? nameof(Propagators.ContextPropagators.Names.Datadog), ',').ToArray();

            PropagationStyleExtract = TrimSplitString(source?.GetString(ConfigurationKeys.PropagationStyleExtract) ?? nameof(Propagators.ContextPropagators.Names.Datadog), ',').ToArray();

            LogSubmissionSettings = new DirectLogSubmissionSettings(source);

            TraceMethods = source?.GetString(ConfigurationKeys.TraceMethods) ??
                           // Default value
                           string.Empty;

            var grpcTags = source?.GetDictionary(ConfigurationKeys.GrpcTags, allowOptionalMappings: true) ??
                                  // default value (empty)
                                  new Dictionary<string, string>();

            // Filter out tags with empty keys or empty values, and trim whitespaces
            GrpcTags = InitializeHeaderTags(grpcTags, headerTagsNormalizationFixEnabled: true);

            var propagationHeaderMaximumLength = source?.GetInt32(ConfigurationKeys.TagPropagation.HeaderMaxLength);

            TagPropagationHeaderMaxLength = propagationHeaderMaximumLength is >= 0 and <= Tagging.TagPropagation.OutgoingPropagationHeaderMaxLength ?
                                             (int)propagationHeaderMaximumLength :
                                             Tagging.TagPropagation.OutgoingPropagationHeaderMaxLength;

            IsActivityListenerEnabled = source?.GetBool(ConfigurationKeys.FeatureFlags.ActivityListenerEnabled) ??
                                // default value
                                false;

            if (IsActivityListenerEnabled)
            {
                // If the activities support is activated, we must enable W3C propagators
                if (!Array.Exists(PropagationStyleExtract, key => string.Equals(key, nameof(Propagators.ContextPropagators.Names.W3C), StringComparison.OrdinalIgnoreCase)))
                {
                    PropagationStyleExtract = PropagationStyleExtract.Concat(nameof(Propagators.ContextPropagators.Names.W3C));
                }

                if (!Array.Exists(PropagationStyleInject, key => string.Equals(key, nameof(Propagators.ContextPropagators.Names.W3C), StringComparison.OrdinalIgnoreCase)))
                {
                    PropagationStyleInject = PropagationStyleInject.Concat(nameof(Propagators.ContextPropagators.Names.W3C));
                }
            }
        }

        /// <summary>
        /// Gets or sets the default environment name applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.Environment"/>
        public string Environment { get; set; }

        /// <summary>
        /// Gets or sets the service name applied to top-level spans and used to build derived service names.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceName"/>
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the version tag applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceVersion"/>
        public string ServiceVersion { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether tracing is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceEnabled"/>
        public bool TraceEnabled { get; set; }

        /// <summary>
        /// Gets or sets the names of disabled integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DisabledIntegrations"/>
        public HashSet<string> DisabledIntegrationNames { get; set; }

        /// <summary>
        /// Gets or sets the transport settings that dictate how the tracer connects to the agent.
        /// </summary>
        public ExporterSettings Exporter { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether default Analytics are enabled.
        /// Settings this value is a shortcut for setting
        /// <see cref="Configuration.IntegrationSettings.AnalyticsEnabled"/> on some predetermined integrations.
        /// See the documentation for more details.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalAnalyticsEnabled"/>
        [Obsolete(DeprecationMessages.AppAnalytics)]
        public bool AnalyticsEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether correlation identifiers are
        /// automatically injected into the logging context.
        /// Default is <c>false</c>, unless <see cref="ConfigurationKeys.DirectLogSubmission.EnabledIntegrations"/>
        /// enables Direct Log Submission.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.LogsInjectionEnabled"/>
        public bool LogsInjectionEnabled
        {
            get => LogSubmissionSettings?.LogsInjectionEnabled ?? false;
            set => LogSubmissionSettings.LogsInjectionEnabled = value;
        }

        /// <summary>
        /// Gets or sets a value indicating the maximum number of traces set to AutoKeep (p1) per second.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceRateLimit"/>
        public int MaxTracesSubmittedPerSecond { get; set; }

        /// <summary>
        /// Gets or sets a value indicating custom sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.CustomSamplingRules"/>
        public string CustomSamplingRules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating a global rate for sampling.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalSamplingRate"/>
        public double? GlobalSamplingRate { get; set; }

        /// <summary>
        /// Gets a collection of <see cref="Integrations"/> keyed by integration name.
        /// </summary>
        public IntegrationSettingsCollection Integrations { get; }

        /// <summary>
        /// Gets or sets the global tags, which are applied to all <see cref="Span"/>s.
        /// </summary>
        public IDictionary<string, string> GlobalTags { get; set; }

        /// <summary>
        /// Gets or sets the map of header keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing HTTP requests.
        /// </summary>
        public IDictionary<string, string> HeaderTags { get; set; }

        /// <summary>
        /// Gets or sets the map of metadata keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing GRPC requests.
        /// </summary>
        public IDictionary<string, string> GrpcTags { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether internal metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        public bool TracerMetricsEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether stats are computed on the tracer side
        /// </summary>
        public bool StatsComputationEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the use
        /// of System.Diagnostics.DiagnosticSource is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <remark>
        /// This value cannot be set in code. Instead,
        /// set it using the <c>DD_TRACE_DIAGNOSTIC_SOURCE_ENABLED</c>
        /// environment variable or in configuration files.
        /// </remark>
        public bool DiagnosticSourceEnabled
        {
            get => GlobalSettings.Source.DiagnosticSourceEnabled;
            set { }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a span context should be created on exiting a successful Kafka
        /// Consumer.Consume() call, and closed on entering Consumer.Consume().
        /// </summary>
        /// <seealso cref="ConfigurationKeys.KafkaCreateConsumerScopeEnabled"/>
        public bool KafkaCreateConsumerScopeEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the updated WCF instrumentation that delays execution
        /// until later in the WCF pipeline when the WCF server exception handling is established.
        /// </summary>
        internal bool DelayWcfInstrumentationEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the diagnostic log at startup is enabled
        /// </summary>
        public bool StartupDiagnosticLogEnabled { get; set; }

        /// <summary>
        /// Gets or sets the maximum length of an outgoing propagation header's value ("x-datadog-tags")
        /// when injecting it into downstream service calls.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TagPropagation.HeaderMaxLength"/>
        /// <remarks>
        /// This value is not used when extracting an incoming propagation header from an upstream service.
        /// </remarks>
        internal int TagPropagationHeaderMaxLength { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the injection propagation style.
        /// </summary>
        internal string[] PropagationStyleInject { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the extraction propagation style.
        /// </summary>
        internal string[] PropagationStyleExtract { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether runtime metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        internal bool RuntimeMetricsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the comma separated list of url patterns to skip tracing.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientExcludedUrlSubstrings"/>
        internal string[] HttpClientExcludedUrlSubstrings { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code that should be marked as errors for server integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpServerErrorStatusCodes"/>
        internal bool[] HttpServerErrorStatusCodes { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code that should be marked as errors for client integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientErrorStatusCodes"/>
        internal bool[] HttpClientErrorStatusCodes { get; set; }

        /// <summary>
        /// Gets configuration values for changing service names based on configuration
        /// </summary>
        internal ServiceNames ServiceNameMappings { get; }

        /// <summary>
        /// Gets or sets a value indicating the size in bytes of the trace buffer
        /// </summary>
        internal int TraceBufferSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the batch interval for the serialization queue, in milliseconds
        /// </summary>
        internal int TraceBatchInterval { get; set; }

        /// <summary>
        /// Gets a value indicating whether the feature flag to enable the updated ASP.NET resource names is enabled
        /// </summary>
        /// <seealso cref="ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled"/>
        internal bool RouteTemplateResourceNamesEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether resource names for ASP.NET and ASP.NET Core spans should be expanded. Only applies
        /// when <see cref="RouteTemplateResourceNamesEnabled"/> is <code>true</code>.
        /// </summary>
        internal bool ExpandRouteTemplatesEnabled { get; }

        /// <summary>
        /// Gets or sets the direct log submission settings.
        /// </summary>
        internal DirectLogSubmissionSettings LogSubmissionSettings { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the trace methods configuration.
        /// </summary>
        internal string TraceMethods { get; set; }

        /// <summary>
        /// Gets a value indicating whether the activity listener is enabled or not.
        /// </summary>
        internal bool IsActivityListenerEnabled { get; }

        /// <summary>
        /// Create a <see cref="TracerSettings"/> populated from the default sources
        /// returned by <see cref="CreateDefaultConfigurationSource"/>.
        /// </summary>
        /// <returns>A <see cref="TracerSettings"/> populated from the default sources.</returns>
        public static TracerSettings FromDefaultSources()
        {
            var source = CreateDefaultConfigurationSource();
            return new TracerSettings(source);
        }

        /// <summary>
        /// Creates a <see cref="IConfigurationSource"/> by combining environment variables,
        /// AppSettings where available, and a local datadog.json file, if present.
        /// </summary>
        /// <returns>A new <see cref="IConfigurationSource"/> instance.</returns>
        public static CompositeConfigurationSource CreateDefaultConfigurationSource()
        {
            return GlobalSettings.CreateDefaultConfigurationSource();
        }

        /// <summary>
        /// Sets the HTTP status code that should be marked as errors for client integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientErrorStatusCodes"/>
        /// <param name="statusCodes">Status codes that should be marked as errors</param>
        public void SetHttpClientErrorStatusCodes(IEnumerable<int> statusCodes)
        {
            HttpClientErrorStatusCodes = ParseHttpCodesToArray(string.Join(",", statusCodes));
        }

        /// <summary>
        /// Sets the HTTP status code that should be marked as errors for server integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpServerErrorStatusCodes"/>
        /// <param name="statusCodes">Status codes that should be marked as errors</param>
        public void SetHttpServerErrorStatusCodes(IEnumerable<int> statusCodes)
        {
            HttpServerErrorStatusCodes = ParseHttpCodesToArray(string.Join(",", statusCodes));
        }

        /// <summary>
        /// Sets the mappings to use for service names within a <see cref="Span"/>
        /// </summary>
        /// <param name="mappings">Mappings to use from original service name (e.g. <code>sql-server</code> or <code>graphql</code>)
        /// as the <see cref="KeyValuePair{TKey, TValue}.Key"/>) to replacement service names as <see cref="KeyValuePair{TKey, TValue}.Value"/>).</param>
        public void SetServiceNameMappings(IEnumerable<KeyValuePair<string, string>> mappings)
        {
            ServiceNameMappings.SetServiceNameMappings(mappings);
        }

        /// <summary>
        /// Create an instance of <see cref="ImmutableTracerSettings"/> that can be used to build a <see cref="Tracer"/>
        /// </summary>
        /// <returns>The <see cref="ImmutableTracerSettings"/> that can be passed to a <see cref="Tracer"/> instance</returns>
        public ImmutableTracerSettings Build()
        {
            return new ImmutableTracerSettings(this);
        }

        private static IDictionary<string, string> InitializeHeaderTags(IDictionary<string, string> configurationDictionary, bool headerTagsNormalizationFixEnabled)
        {
            var headerTags = new Dictionary<string, string>();

            foreach (var kvp in configurationDictionary)
            {
                var headerName = kvp.Key;
                var providedTagName = kvp.Value;
                if (string.IsNullOrWhiteSpace(headerName))
                {
                    continue;
                }

                // The user has not provided a tag name. The normalization will happen later, when adding the prefix.
                if (string.IsNullOrEmpty(providedTagName))
                {
                    headerTags.Add(headerName.Trim(), string.Empty);
                }
                else if (headerTagsNormalizationFixEnabled && providedTagName.TryConvertToNormalizedTagName(normalizePeriods: false, out var normalizedTagName))
                {
                    // If the user has provided a tag name, then we don't normalize periods in the provided tag name
                    headerTags.Add(headerName.Trim(), normalizedTagName);
                }
                else if (!headerTagsNormalizationFixEnabled && providedTagName.TryConvertToNormalizedTagName(normalizePeriods: true, out var normalizedTagNameNoPeriods))
                {
                    // Back to the previous behaviour if the flag is set
                    headerTags.Add(headerName.Trim(), normalizedTagNameNoPeriods);
                }
            }

            return headerTags;
        }

        // internal for testing
        internal static IEnumerable<string> TrimSplitString(string textValues, char separator)
        {
            var values = textValues.Split(separator);

            for (var i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    yield return values[i].Trim();
                }
            }
        }

        internal static bool[] ParseHttpCodesToArray(string httpStatusErrorCodes)
        {
            bool[] httpErrorCodesArray = new bool[600];

            void TrySetValue(int index)
            {
                if (index >= 0 && index < httpErrorCodesArray.Length)
                {
                    httpErrorCodesArray[index] = true;
                }
            }

            string[] configurationsArray = httpStatusErrorCodes.Replace(" ", string.Empty).Split(',');

            foreach (string statusConfiguration in configurationsArray)
            {
                int startStatus;

                // Checks that the value about to be used follows the `401-404` structure or single 3 digit number i.e. `401` else log the warning
                if (!Regex.IsMatch(statusConfiguration, @"^\d{3}-\d{3}$|^\d{3}$"))
                {
                    Log.Warning("Wrong format '{0}' for DD_HTTP_SERVER/CLIENT_ERROR_STATUSES configuration.", statusConfiguration);
                }

                // If statusConfiguration equals a single value i.e. `401` parse the value and save to the array
                else if (int.TryParse(statusConfiguration, out startStatus))
                {
                    TrySetValue(startStatus);
                }
                else
                {
                    string[] statusCodeLimitsRange = statusConfiguration.Split('-');

                    startStatus = int.Parse(statusCodeLimitsRange[0]);
                    int endStatus = int.Parse(statusCodeLimitsRange[1]);

                    if (endStatus < startStatus)
                    {
                        startStatus = endStatus;
                        endStatus = int.Parse(statusCodeLimitsRange[0]);
                    }

                    for (int statusCode = startStatus; statusCode <= endStatus; statusCode++)
                    {
                        TrySetValue(statusCode);
                    }
                }
            }

            return httpErrorCodesArray;
        }
    }
}
