// <copyright file="Attack.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Datadog.Trace.AppSec.Transports.Http;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.EventModel
{
    internal class Attack : AppSecEvent
    {
        [JsonProperty("event_type")]
        public const string EventType = "appsec.threat.attack";

        [JsonProperty("rule")]
        public Rule Rule { get; set; }

        [JsonProperty("rule_match")]
        public RuleMatch RuleMatch { get; set; }

        [JsonProperty("context")]
        public Context Context { get; set; }

        public static Attack From(Waf.ReturnTypes.Managed.Return result, Trace.Span span, Transport.ITransport transport, string customIpHeader, IEnumerable<string> extraHeaders)
        {
            var resultData = result.ResultData[0];
            var ruleMatch = resultData.RuleMatches.FirstOrDefault();
            var parameter = ruleMatch?.Parameters?.FirstOrDefault();
            var request = transport.Request();
            var headersIpAndPort = RequestHeadersHelper.ExtractHeadersIpAndPort(transport.GetHeader, customIpHeader, extraHeaders,  transport.IsSecureConnection, new IpInfo(request.RemoteIp, request.RemotePort));
            request.Headers = headersIpAndPort.HeadersToSend;
            var frameworkDescription = FrameworkDescription.Instance;
            var attack = new Attack
            {
                EventId = Guid.NewGuid().ToString(),
                Context = new Context
                {
                    Actor = new Actor { Ip = new Ip { Address = headersIpAndPort.IpInfo.IpAddress } },
                    Host = new Host
                    {
                        OsType = frameworkDescription.OSPlatform,
                        Hostname = Dns.GetHostName()
                    },
                    Http = new Http
                    {
                        Request = request,
                        Response = transport.Response(result.Blocked)
                    },
                    Service = new Service { Environment = CorrelationIdentifier.Env },
                    Tracer = new Tracer
                    {
                        RuntimeType = frameworkDescription.Name,
                        RuntimeVersion = frameworkDescription.ProductVersion,
                    }
                },
                Blocked = result.Blocked,
                Rule = new Rule { Name = resultData.Rule.Name, Id = resultData.Rule.Id },
                DetectedAt = DateTime.UtcNow,
                RuleMatch = new RuleMatch
                {
                    Operator = ruleMatch?.Operator,
                    OperatorValue = string.IsNullOrEmpty(ruleMatch?.OperatorValue) ? parameter?.Value : ruleMatch?.OperatorValue,
                    Highlight = new[] { parameter?.Highlight.FirstOrDefault() },
                    Parameters = new[] { new Parameter { Name = parameter?.Address, Value = parameter?.Value } }
                },
                Type = resultData.Rule?.Tags?.Type
            };
            if (string.IsNullOrEmpty(attack.RuleMatch.Highlight[0]))
            {
                attack.RuleMatch.Highlight[0] = attack.RuleMatch.OperatorValue;
            }

            if (span != null)
            {
                attack.Context.Span = new Span { Id = span.SpanId };
                attack.Context.Trace = new Span { Id = span.TraceId };
                attack.Context.Service.Name = span.ServiceName;
            }

            return attack;
        }
    }
}
