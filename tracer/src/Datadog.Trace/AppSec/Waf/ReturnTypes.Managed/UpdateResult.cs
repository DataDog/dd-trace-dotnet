// <copyright file="UpdateResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal class UpdateResult
    {
        internal UpdateResult(IntPtr diagnostics, bool success, bool unusableRules = false)
        {
            if (diagnostics != IntPtr.Zero)
            {
                Dictionary<string, object>? rules = null;
                var diagObject = new Obj(diagnostics);
                if (diagObject.ArgsType == ObjType.Invalid)
                {
                    Errors = new Dictionary<string, object> { { "diagnostics-error", "Waf didn't provide a valid diagnostics object at initialization, most likely due to an older waf version < 1.11.0" } };
                }
                else
                {
                    var diagnosticsData = (Dictionary<string, object>)Encoder.Decode(diagObject);
                    if (diagnosticsData.Count > 0)
                    {
                        object? rulesObj = null;
                        var valueExist = diagnosticsData.TryGetValue("rules", out rulesObj);
                        if (!valueExist)
                        {
                            valueExist = diagnosticsData.TryGetValue("rules_override", out rulesObj);
                            if (!valueExist)
                            {
                                Errors = new Dictionary<string, object> { { "diagnostics-error", "Waf could not provide diagnostics on rules or rule_overrides" } };
                            }
                        }

                        if (rulesObj != null)
                        {
                            rules = rulesObj as Dictionary<string, object>;
                            if (rules == null)
                            {
                                Errors = new Dictionary<string, object> { { "diagnostics-error", "Waf could not provide diagnostics on rules as a dictionary key-value" } };
                            }
                        }

                        if (diagnosticsData.TryGetValue("ruleset_version", out var ruleFileVersion))
                        {
                            RuleFileVersion = Convert.ToString(ruleFileVersion);
                        }
                    }
                }

                FailedToLoadRules = (ushort)(rules != null ? ((object[])rules["failed"]).Length : 0);
                LoadedRules = (ushort)(rules != null ? ((object[])rules["loaded"]).Length : 0);
                Errors = Errors ?? (Dictionary<string, object>)rules!["errors"];

                if (Errors != null && Errors.Count > 0)
                {
                    HasErrors = true;
                    ErrorMessage = JsonConvert.SerializeObject(Errors);
                }
            }

            Success = success;
            UnusableRules = unusableRules;
        }

        internal bool Success { get; }

        internal bool UnusableRules { get; }

        internal ushort? FailedToLoadRules { get; }

        /// <summary>
        /// Gets the number of rules successfully loaded
        /// </summary>
        internal ushort? LoadedRules { get; }

        internal IReadOnlyDictionary<string, object>? Errors { get; }

        internal string? ErrorMessage { get; }

        internal bool? HasErrors { get; }

        internal string? RuleFileVersion { get; }

        public static UpdateResult FromUnusableRules() => new UpdateResult(IntPtr.Zero, false, true);

        public static UpdateResult FromFailed() => new UpdateResult(IntPtr.Zero, false);
    }
}
