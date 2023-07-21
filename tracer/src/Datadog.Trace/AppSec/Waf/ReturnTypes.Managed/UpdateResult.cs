// <copyright file="UpdateResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal class UpdateResult
    {
        internal UpdateResult(DdwafRuleSetInfo? ruleSetInfo, bool success, bool unusableRules = false)
        {
            if (ruleSetInfo != null)
            {
                var errors = ruleSetInfo.Errors.Decode();
                HasErrors = errors.Count > 0;
                Errors = errors;
                FailedToLoadRules = ruleSetInfo.Failed;
                LoadedRules = ruleSetInfo.Loaded;
                RuleFileVersion = Marshal.PtrToStringAnsi(ruleSetInfo.Version);
                if (errors.Count > 0)
                {
                    HasErrors = true;
                    ErrorMessage = JsonConvert.SerializeObject(errors);
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

        internal IReadOnlyDictionary<string, string[]>? Errors { get; }

        internal string? ErrorMessage { get; }

        internal bool? HasErrors { get; }

        internal string? RuleFileVersion { get; }

        public static UpdateResult FromUnusableRules() => new UpdateResult(null, false, true);

        public static UpdateResult FromFailed() => new UpdateResult(null, false);
    }
}
