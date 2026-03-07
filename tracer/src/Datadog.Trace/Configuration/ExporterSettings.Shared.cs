// <copyright file="ExporterSettings.Shared.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Methods of ExporterSettings shared with other projects
    /// </summary>
    public partial class ExporterSettings
    {
        /// <summary>
        /// The default host value for <see cref="AgentUri"/>.
        /// </summary>
        public const string DefaultAgentHost = "localhost";

        /// <summary>
        /// The default port value for <see cref="AgentUri"/>.
        /// </summary>
        public const int DefaultAgentPort = 8126;

        /// <summary>
        /// The default port value for OTLP gRPC protocols.
        /// </summary>
        public const int DefaultOtlpGrpcPort = 4317;

        /// <summary>
        /// The default port value for OTLP HTTP protocols.
        /// </summary>
        public const int DefaultOtlpHttpPort = 4318;

        /// <summary>
        /// Prefix for unix domain sockets.
        /// </summary>
        internal const string UnixDomainSocketPrefix = "unix://";

        /// <summary>
        /// Default traces UDS path.
        /// </summary>
        internal const string DefaultTracesUnixDomainSocket = "/var/run/datadog/apm.socket";

        private TraceTransportSettings GetTraceTransport(string? agentUri, string? tracesPipeName, string? agentHost, int? agentPort, string? tracesUnixDomainSocketPath)
        {
            var origin = ConfigurationOrigins.Default; // default because only called from constructor
            var encoding = TracesEncoding.DatadogV0_4;
            var otlpProtocol = OtlpProtocol.HttpProtobuf; // default value that is unused when transmitting Datadog-encoded traces

            // Check the parameters in order of precedence
            // For some cases, we allow falling back on another configuration (eg invalid url as the application will need to be restarted to fix it anyway).
            // For other cases (eg a configured unix domain socket path not found), we don't fallback as the problem could be fixed outside the application.
            if (!string.IsNullOrEmpty(agentUri))
            {
                if (TryGetAgentUriAndTransport(agentUri!, origin, encoding, otlpProtocol, out var settings))
                {
                    return settings;
                }
            }

            if (!string.IsNullOrEmpty(tracesPipeName))
            {
                RecordTraceTransport(nameof(TracesTransportType.WindowsNamedPipe), origin);

                // The Uri isn't needed anymore in that case, just populating it for retro compatibility.
                if (!Uri.TryCreate($"http://{agentHost ?? DefaultAgentHost}:{agentPort ?? DefaultAgentPort}", UriKind.Absolute, out var uri))
                {
                    // fallback so AgentUri is always non-null
                    uri = CreateDefaultUri();
                }

                return new TraceTransportSettings(
                    TracesTransportType.WindowsNamedPipe,
                    GetAgentUriReplacingLocalhost(uri, origin),
                    PipeName: tracesPipeName);
            }

            // This property shouldn't have been introduced. We need to remove it as part of 3.0
            // But while it's here, we need to handle it properly
            if (!string.IsNullOrEmpty(tracesUnixDomainSocketPath))
            {
#if NETCOREAPP3_1_OR_GREATER
                if (TryGetAgentUriAndTransport(UnixDomainSocketPrefix + tracesUnixDomainSocketPath, origin, encoding, otlpProtocol, out var settings))
                {
                    return settings;
                }
#else
                // .NET Core 2.1 and .NET FX don't support Unix Domain Sockets
                ValidationWarnings.Add($"Found UDS configuration {ConfigurationKeys.TracesUnixDomainSocketPath}, but current runtime doesn't support UDS, so ignoring it.");
                _telemetry.Record(
                    ConfigurationKeys.TracesUnixDomainSocketPath,
                    tracesUnixDomainSocketPath,
                    recordValue: true,
                    origin,
                    TelemetryErrorCode.UdsOnUnsupportedPlatform);
#endif
            }

            if ((agentPort != null && agentPort != 0) || agentHost != null)
            {
                // Agent port is set to zero in places like AAS where it's needed to prevent port conflict
                // The agent will fail to start if it can not bind a port, so we need to override 8126 to prevent port conflict
                // Port 0 means it will pick some random available port

                if (TryGetAgentUriAndTransport(agentHost ?? DefaultAgentHost, agentPort ?? DefaultAgentPort, encoding, otlpProtocol, out var settings))
                {
                    return settings;
                }
            }

            // .NET Core 2.1 and .NET FX don't support Unix Domain Sockets, so we don't care if the file already exists
#if NETCOREAPP3_1_OR_GREATER
            if (_fileExists(DefaultTracesUnixDomainSocket))
            {
                // setting the urls as well for retro compatibility in the almost impossible case where someone
                // used this config and accessed the AgentUri property as well (to avoid a potential null ref)
                // Using Get not TryGet because we know this is a valid Uri and ensures _agentUri is always non-null
                return GetAgentUriAndTransport(new Uri(UnixDomainSocketPrefix + DefaultTracesUnixDomainSocket), origin, encoding, otlpProtocol);
            }
#endif

            ValidationWarnings.Add("No transport configuration found, using default values");

            // we know this URL is valid so don't use TrySet, otherwise can't guarantee _agentUri is non null
            return GetAgentUriAndTransport(CreateDefaultUri(), origin, encoding, otlpProtocol);
        }

        private TraceTransportSettings GetOtlpTracesTransport(string? signalEndpoint, string? generalEndpoint, string? signalProtocol, string? generalProtocol)
            => GetOtlpTransport(signalEndpoint, generalEndpoint, signalProtocol, generalProtocol, "v1/traces", OtlpProtocol.HttpJson); // TODO: Make default HttpProtobuf

        private TraceTransportSettings GetOtlpMetricsTransport(string? signalEndpoint, string? generalEndpoint, string? signalProtocol, string? generalProtocol)
            => GetOtlpTransport(signalEndpoint, generalEndpoint, signalProtocol, generalProtocol, "v1/metrics", OtlpProtocol.HttpProtobuf);

        private TraceTransportSettings GetOtlpTransport(string? signalEndpoint, string? generalEndpoint, string? signalProtocol, string? generalProtocol, string defaultHttpRelativePath, OtlpProtocol defaultProtocol)
        {
            var origin = ConfigurationOrigins.Default; // default because only called from constructor

            var protocol = (signalProtocol ?? generalProtocol) switch
            {
                "grpc" => OtlpProtocol.Grpc,
                "http/protobuf" => OtlpProtocol.HttpProtobuf,
                "http/json" => OtlpProtocol.HttpJson,
                _ => defaultProtocol,
            };
            var encoding = protocol switch
            {
                OtlpProtocol.Grpc => TracesEncoding.OtlpProtobuf,
                OtlpProtocol.HttpProtobuf => TracesEncoding.OtlpProtobuf,
                _ => TracesEncoding.OtlpJson,
            };

            if (!string.IsNullOrEmpty(signalEndpoint))
            {
                if (TryGetAgentUriAndTransport(signalEndpoint!, origin, encoding, protocol, out var settings))
                {
                    return settings;
                }
            }

            if (!string.IsNullOrEmpty(generalEndpoint))
            {
                if (protocol == OtlpProtocol.Grpc && TryGetAgentUriAndTransport(generalEndpoint!, origin, encoding, protocol, out var grpcSettings))
                {
                    return grpcSettings;
                }
                else if (protocol != OtlpProtocol.Grpc)
                {
                    if (signalEndpoint!.EndsWith("/") && TryGetAgentUriAndTransport($"{generalEndpoint!}{defaultHttpRelativePath}", origin, encoding, protocol, out var httpSettings))
                    {
                        return httpSettings;
                    }
                    else if (!signalEndpoint!.EndsWith("/") && TryGetAgentUriAndTransport($"{generalEndpoint!}/{defaultHttpRelativePath}", origin, encoding, protocol, out var httpAdditionalSlashSettings))
                    {
                        return httpAdditionalSlashSettings;
                    }
                }
            }

            ValidationWarnings.Add("No transport configuration found, using default values");

            // we know this URL is valid so don't use TrySet, otherwise can't guarantee _agentUri is non null
            return GetAgentUriAndTransport(CreateDefaultOtlpUri(protocol, defaultHttpRelativePath), origin, encoding, protocol);
        }

        private bool TryGetAgentUriAndTransport(string host, int port, TracesEncoding encoding, OtlpProtocol otlpProtocol, out TraceTransportSettings settings)
        {
            return TryGetAgentUriAndTransport($"http://{host}:{port}", ConfigurationOrigins.Default, encoding, otlpProtocol, out settings); // default because only called from constructor
        }

        private bool TryGetAgentUriAndTransport(string url, ConfigurationOrigins origin, TracesEncoding encoding, OtlpProtocol otlpProtocol, out TraceTransportSettings settings)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                ValidationWarnings.Add($"The Uri: '{url}' is not valid. It won't be taken into account to send traces. Note that only absolute urls are accepted.");
                settings = default;
                return false;
            }

            settings = GetAgentUriAndTransport(uri, ConfigurationOrigins.Default, encoding, otlpProtocol); // default because only called from constructor
            return true;
        }

        private TraceTransportSettings GetAgentUriAndTransport(Uri uri, ConfigurationOrigins origin, TracesEncoding encoding, OtlpProtocol otlpProtocol)
        {
            TracesTransportType transport;
            string? udsPath;
            if (uri.OriginalString.StartsWith(UnixDomainSocketPrefix, StringComparison.OrdinalIgnoreCase))
            {
#if NETCOREAPP3_1_OR_GREATER
                transport = TracesTransportType.UnixDomainSocket;
                udsPath = uri.PathAndQuery;

                var absoluteUri = uri.AbsoluteUri.Replace(UnixDomainSocketPrefix, string.Empty);
                bool potentiallyInvalid = false;
                if (!Path.IsPathRooted(absoluteUri))
                {
                    potentiallyInvalid = true;
                    ValidationWarnings.Add($"The provided Uri {uri} contains a relative path which may not work. This is the path to the socket that will be used: {uri.PathAndQuery}");
                }

                // check if the file exists to warn the user.
                if (!_fileExists(uri.PathAndQuery))
                {
                    // We don't fallback in that case as the file could be mounted separately.
                    potentiallyInvalid = true;
                    ValidationWarnings.Add($"The socket provided {uri.PathAndQuery} cannot be found. The tracer will still rely on this socket to send traces.");
                }

                RecordTraceTransport(nameof(TracesTransportType.UnixDomainSocket), origin);
                _telemetry.Record(
                    ConfigurationKeys.TracesUnixDomainSocketPath,
                    TracesUnixDomainSocketPath,
                    recordValue: true,
                    origin,
                    potentiallyInvalid ? TelemetryErrorCode.PotentiallyInvalidUdsPath : null);
#else
                // .NET Core 2.1 and .NET FX don't support Unix Domain Sockets, but it's _explicitly_ being
                // configured here, so warn the user, and switch to using the default transport instead.
                ValidationWarnings.Add($"The provided Uri {uri} represents a Unix Domain Socket (UDS), but the current runtime doesn't support UDS. Falling back to the default TCP transport.");
                _telemetry.Record(
                    ConfigurationKeys.AgentUri,
                    uri.ToString(),
                    recordValue: true,
                    origin,
                    TelemetryErrorCode.UdsOnUnsupportedPlatform);
                return GetAgentUriAndTransport(CreateDefaultUri(), ConfigurationOrigins.Calculated, encoding, otlpProtocol);
#endif
            }
            else
            {
                transport = TracesTransportType.Default;
                udsPath = null;
                RecordTraceTransport(nameof(TracesTransportType.Default), origin);
            }

            var agentUri = GetAgentUriReplacingLocalhost(uri, origin);
            return new(transport, agentUri, UdsPath: udsPath, Encoding: encoding, OtlpProtocol: otlpProtocol);
        }

        private Uri GetAgentUriReplacingLocalhost(Uri uri, ConfigurationOrigins origin)
        {
            Uri agentUri;
            if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                // Replace localhost with 127.0.0.1 to avoid DNS resolution.
                // When ipv6 is enabled, localhost is first resolved to ::1, which fails
                // because the trace agent is only bound to ipv4.
                // This causes delays when sending traces.
                var builder = new UriBuilder(uri) { Host = "127.0.0.1" };
                agentUri = builder.Uri;
            }
            else
            {
                agentUri = uri;
            }

            _telemetry.Record(ConfigurationKeys.AgentUri, agentUri.ToString(), recordValue: true, origin);
            return agentUri;
        }

        private Uri CreateDefaultUri() => new Uri($"http://{DefaultAgentHost}:{DefaultAgentPort}");

        private Uri CreateDefaultOtlpUri(OtlpProtocol protocol, string defaultHttpRelativePath) => protocol switch
        {
            OtlpProtocol.Grpc => new Uri($"http://{DefaultAgentHost}:{DefaultOtlpGrpcPort}"),
            OtlpProtocol.HttpProtobuf or OtlpProtocol.HttpJson => new Uri($"http://{DefaultAgentHost}:{DefaultOtlpHttpPort}/{defaultHttpRelativePath}"),
            _ => throw new ArgumentException($"Invalid protocol: {protocol}"),
        };

        private readonly record struct TraceTransportSettings(TracesTransportType Transport, Uri AgentUri, string? UdsPath = null, string? PipeName = null, TracesEncoding Encoding = TracesEncoding.DatadogV0_4, OtlpProtocol OtlpProtocol = OtlpProtocol.HttpProtobuf);
    }
}
