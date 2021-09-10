// <copyright file="Attack.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
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

        public static Attack From(Waf.ReturnTypes.Managed.Return result, Trace.Span span, Transport.ITransport transport)
        {
            var ruleMatch = result.ResultData.Filter[0];
            var request = transport.Request();
            var frameworkDescription = FrameworkDescription.Instance;
            var attack = new Attack
            {
                EventId = Guid.NewGuid().ToString(),
                Context = new Context()
                {
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
                    Actor = new Actor { Ip = new Ip { Address = request.RemoteIp } },
                    Tracer = new Tracer
                    {
                        RuntimeType = frameworkDescription.Name,
                        RuntimeVersion = frameworkDescription.ProductVersion,
                    }
                },
                Blocked = result.Blocked,
                Rule = new Rule { Name = result.ResultData.Flow, Id = result.ResultData.Rule },
                DetectedAt = DateTime.UtcNow,
                RuleMatch = new RuleMatch
                {
                    Operator = ruleMatch.Operator,
                    OperatorValue = ruleMatch.OperatorValue,
                    Highlight = new string[] { ruleMatch.MatchStatus },
                    Parameters = new Parameter[] { new Parameter { Name = ruleMatch.BindingAccessor, Value = ruleMatch.ResolvedValue } }
                },
                Type = result.ResultData.Flow
            };
            if (span != null)
            {
                attack.Context.Span = new Span { Id = span.SpanId };
                attack.Context.Trace = new Span { Id = span.TraceId };
                attack.Context.Service = new Service { Name = span.ServiceName };
            }

            return attack;
        }
    }
}
