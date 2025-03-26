// <copyright file="UpdateResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal record UpdateResult
    {
        private UpdateResult(DdwafObjectStruct? diagObject, bool success, IntPtr builderHandle = default(IntPtr), IntPtr wafHandle = default(IntPtr), WafLibraryInvoker? invoker = null, IEncoder? encoder = null)
        {
            WafLibraryInvoker = invoker;
            Encoder = encoder;
            WafBuilderHandle = builderHandle;
            WafHandle = wafHandle;

            if (diagObject != null)
            {
                ReportedDiagnostics = DiagnosticResultUtils.ExtractReportedDiagnostics(diagObject.Value, false);

                if (ReportedDiagnostics.Errors is { Count: > 0 })
                {
                    HasErrors = true;
                    ErrorMessage = JsonConvert.SerializeObject(ReportedDiagnostics.Errors);
                }

                if (ReportedDiagnostics.Warnings is { Count: > 0 })
                {
                    HasWarnings = true;
                }
            }

            Success = success;
        }

        private UpdateResult(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        internal bool Success { get; }

        internal WafLibraryInvoker? WafLibraryInvoker { get; }

        internal IEncoder? Encoder { get; }

        internal IntPtr WafBuilderHandle { get; }

        internal IntPtr WafHandle { get; }

        internal ReportedDiagnostics ReportedDiagnostics { get; } = new();

        internal string ErrorMessage { get; } = string.Empty;

        internal bool HasErrors { get; }

        internal bool HasWarnings { get; }

        internal string RuleFileVersion => ReportedDiagnostics.RulesetVersion;

        internal IReadOnlyDictionary<string, object>? Errors => ReportedDiagnostics.Errors;

        internal IReadOnlyDictionary<string, object>? Warnings => ReportedDiagnostics.Warnings;

        public static UpdateResult FromFailed() => new(null, false);

        public static UpdateResult FromFailed(DdwafObjectStruct diagObj) => new(diagObj, false);

        public static UpdateResult FromSuccess(DdwafObjectStruct diagObj, IntPtr builderHandle, IntPtr wafHandle, WafLibraryInvoker invoker, IEncoder encoder) => new(diagObj, true, builderHandle, wafHandle, invoker, encoder);

        internal static UpdateResult FromException(Exception e) => new(e.Message);

        internal static UpdateResult FromFailed(string message) => new(message);
    }
}
