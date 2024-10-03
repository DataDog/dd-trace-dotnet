// <copyright file="UpdateResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal class UpdateResult
    {
        private UpdateResult(DdwafObjectStruct? diagObject, bool success, bool unusableRules = false, bool nothingToUpdate = false)
        {
            if (diagObject != null)
            {
                var reportedDiag = DiagnosticResultUtils.ExtractReportedDiagnostics(diagObject.Value, false);

                FailedToLoadRules = reportedDiag.FailedCount;
                LoadedRules = reportedDiag.LoadedCount;
                Errors = reportedDiag.Errors;
                RuleFileVersion = reportedDiag.RulesetVersion;

                if (Errors is { Count: > 0 })
                {
                    HasErrors = true;
                    ErrorMessage = JsonConvert.SerializeObject(Errors);
                }
            }

            Success = success;
            UnusableRules = unusableRules;
            NothingToUpdate = nothingToUpdate;
        }

        internal bool Success { get; }

        internal bool UnusableRules { get; }

        public bool NothingToUpdate { get; }

        internal ushort? FailedToLoadRules { get; }

        /// <summary>
        /// Gets the number of rules successfully loaded
        /// </summary>
        internal ushort? LoadedRules { get; }

        internal IReadOnlyDictionary<string, object>? Errors { get; }

        internal string? ErrorMessage { get; }

        internal bool HasErrors { get; }

        internal string? RuleFileVersion { get; }

        public static UpdateResult FromUnusableRules() => new(null, false, true);

        public static UpdateResult FromNothingToUpdate() => new(null, true, nothingToUpdate: true);

        public static UpdateResult FromFailed() => new(null, false);

        public static UpdateResult FromFailed(DdwafObjectStruct diagObj) => new(diagObj, false);

        public static UpdateResult FromSuccess(DdwafObjectStruct diagObj) => new(diagObj, true);
    }
}
