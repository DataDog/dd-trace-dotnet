// <copyright file="DnsClientCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Globalization;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.DnsClient
{
    internal static class DnsClientCommon
    {
        internal const string OperationName = "dns.query";
        internal const string IntegrationName = nameof(Configuration.IntegrationId.DnsClient);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.DnsClient;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DnsClientCommon));

        public static Scope? CreateScope(Tracer tracer, object? question, object? servers)
        {
            var perTraceSettings = tracer.CurrentTraceSettings;
            if (!perTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope? scope = null;

            try
            {
                var (serviceName, serviceNameSource) = perTraceSettings.Schema.Client.GetServiceNameMetadata(ClientSchema.Component.DnsClient);
                var tags = perTraceSettings.Schema.Client.CreateDnsClientTags();

                scope = tracer.StartActiveInternal(OperationName, tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
                var span = scope.Span;
                span.Type = SpanTypes.Dns;

                string? questionName = null;
                if (question is not null && question.TryDuckCast<IDnsQuestion>(out var dnsQuestion))
                {
                    questionName = dnsQuestion.QueryName?.ToString();
                    tags.QuestionName = questionName;
                    tags.QuestionType = dnsQuestion.QuestionType?.ToString();
                    tags.QuestionClass = dnsQuestion.QuestionClass?.ToString();
                }

                // Capture the target name server so we still have out.host on error paths
                // (where no response is available to read the answering server from).
                if (servers is IEnumerable serversEnumerable)
                {
                    foreach (var server in serversEnumerable)
                    {
                        if (server is not null && server.TryDuckCast<INameServer>(out var nameServer))
                        {
                            tags.OutHost = nameServer.Address;
                            tags.DestinationPort = nameServer.Port.ToString(CultureInfo.InvariantCulture);
                        }

                        break;
                    }
                }

                span.ResourceName = StringUtil.IsNullOrEmpty(questionName) ? OperationName : questionName;

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: false);
                perTraceSettings.Schema.RemapPeerService(tags);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        public static void PopulateResponseTags(Scope? scope, object? response)
        {
            if (scope is null || response is null)
            {
                return;
            }

            try
            {
                if (!response.TryDuckCast<IDnsQueryResponse>(out var dnsResponse))
                {
                    return;
                }

                if (scope.Span.Tags is DnsClientTags tags)
                {
                    var responseCode = dnsResponse.Header?.ResponseCode;
                    tags.ResponseCode = responseCode?.ToString();

                    if (dnsResponse.Answers is not null)
                    {
                        tags.AnswerCount = dnsResponse.Answers.Count.ToString(CultureInfo.InvariantCulture);
                    }

                    if (dnsResponse.NameServer is not null && dnsResponse.NameServer.TryDuckCast<INameServer>(out var nameServer))
                    {
                        tags.OutHost = nameServer.Address;
                        tags.DestinationPort = nameServer.Port.ToString(CultureInfo.InvariantCulture);
                    }

                    // When DnsQuerySettings.ThrowDnsErrors is false (the default), a failed
                    // query (e.g. NXDOMAIN, SERVFAIL) surfaces as a response with a non-success
                    // ResponseCode rather than as a thrown exception, so it must be detected here.
                    // The underlying DnsClient.DnsHeaderResponseCode enum uses NoError = 0 for success.
                    if (responseCode is not null && Convert.ToInt32(responseCode, CultureInfo.InvariantCulture) != 0)
                    {
                        var span = scope.Span;
                        span.Error = true;
                        span.SetTag(Tags.ErrorType, "DnsClient.DnsResponseException");
                        span.SetTag(Tags.ErrorMsg, $"DNS query returned response code: {tags.ResponseCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error populating DNS response tags.");
            }
        }
    }
}
