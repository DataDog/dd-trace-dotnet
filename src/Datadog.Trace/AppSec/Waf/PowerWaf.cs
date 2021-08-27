// <copyright file="PowerWaf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf
{
    internal class PowerWaf : IPowerWaf
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(PowerWaf));

        private readonly Rule rule;
        private bool disposed = false;

        private PowerWaf(Rule rule)
        {
            this.rule = rule;
        }

        ~PowerWaf()
        {
            Dispose(false);
        }

        public Version Version
        {
            get
            {
                var ver = Native.pw_getVersion();
                return new Version(ver.Major, ver.Minor, ver.Patch);
            }
        }

        public static PowerWaf Initialize()
        {
            var rule = NewRule();
            return rule == null ? null : new PowerWaf(rule);
        }

        public IAdditiveContext CreateAdditiveContext()
        {
            var handle = Native.pw_initAdditiveH(rule.Handle);
            return new AdditiveContext(handle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            rule?.Dispose();
        }

        private static Args StaticRuleSet()
        {
            // This yaml rule is equivalent to the Args data structure generated below.
            // This is temparary measure, we will soon incorporate a default yaml rule set

            // version: "0.1"
            // events:
            //   - id: crs-942-290
            //     name: Finds basic MongoDB SQL injection attempts
            //     tags:
            //       crs_id: "942290"
            //       type: nosqli
            //     conditions:
            //       - operation: match_regex
            //         parameters:
            //           inputs:
            //             - http.server.cookies
            //             - http.server.query
            //             - http.server.body
            //             - http.server.path_params
            //           options:
            //             case_sensitive: true
            //             min_length: 5
            //           regex: (?i:(?:\[\$(?:ne|eq|lte?|gte?|n?in|mod|all|size|exists|type|slice|x?or|div|like|between|and)\]))
            //     transformers: []
            //     action: record

            var x = new Dictionary<string, object>()
            {
                { "version", "0.1" },
                {
                    "events",
                    new List<object>()
                    {
                        new Dictionary<string, object>()
                        {
                            { "id", "crs-942-290" },
                            {
                                "tags",
                                new Dictionary<string, object>()
                                {
                                    { "crs_id", "942290" },
                                    { "type", "nosqli" }
                                }
                            },
                            {
                                "conditions",
                                new List<object>()
                                {
                                    new Dictionary<string, object>()
                                    {
                                        { "operation", "match_regex" },
                                        {
                                            "parameters",
                                            new Dictionary<string, object>()
                                            {
                                                {
                                                    "inputs",
                                                    new List<object>()
                                                    {
                                                        "server.request.query",
                                                        "server.request.uri.raw",
                                                        "server.request.cookies",
                                                        "server.request.headers.no_cookies",
                                                    }
                                                },
                                                {
                                                    "options",
                                                    new Dictionary<string, object>()
                                                    {
                                                        { "case_sensitive", "true" },
                                                        { "min_length", "5" },
                                                    }
                                                },
                                                { "regex", @"(?i:(?:\[\$(?:ne|eq|lte?|gte?|n?in|mod|all|size|exists|type|slice|x?or|div|like|between|and)\]))" }
                                            }
                                        },
                                    }
                                }
                            },
                            { "transformers", new List<object>() },
                            { "action", "record" }
                        }
                    }
                },
            };

            return Encoder.Encode(x);
        }

        private static Rule NewRule()
        {
            try
            {
                string message = null;
                PWConfig args = default;

                var rules = StaticRuleSet();

                var ruleHandle = Native.pw_initH(rules.RawArgs, ref args);

                if (ruleHandle == IntPtr.Zero)
                {
                    Log.Error("Failed to create rules: {Message}", message);
                    return null;
                }
                else
                {
                    Log.Information("Rules successfully created: {Message}", message);
                }

                return new Rule(ruleHandle);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading the power WAF rules");
                return null;
            }
        }
    }
}
