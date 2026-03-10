// <copyright file="UpdateResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal sealed class UpdateResult
    {
        private UpdateResult(in DdwafObjectStruct diagObject, IntPtr builderHandle = default(IntPtr), IntPtr wafHandle = default(IntPtr), WafLibraryInvoker? invoker = null, IEncoder? encoder = null)
        {
            WafLibraryInvoker = invoker;
            Encoder = encoder;
            WafBuilderHandle = builderHandle;
            WafHandle = wafHandle;

            if (diagObject.Type != DDWAF_OBJ_TYPE.DDWAF_OBJ_NULL && diagObject.Type != DDWAF_OBJ_TYPE.DDWAF_OBJ_INVALID)
            {
                ReportedDiagnostics = DiagnosticResultUtils.ExtractReportedDiagnostics(diagObject, false);

                if (ReportedDiagnostics.Rules.Errors is { Count: > 0 })
                {
                    HasRuleErrors = true;
                    ErrorMessage = JsonHelper.SerializeObject(ReportedDiagnostics.Rules.Errors);
                }
            }
        }

        private UpdateResult(string errorMessage, in DdwafObjectStruct diagObject, IntPtr builderHandle = default(IntPtr), WafLibraryInvoker? invoker = null, IEncoder? encoder = null)
            : this(diagObject, builderHandle, IntPtr.Zero, invoker, encoder)
        {
            ErrorMessage = ErrorMessage != string.Empty ? string.Join(Environment.NewLine, errorMessage, ErrorMessage) : errorMessage;
        }

        internal bool Success => WafHandle != IntPtr.Zero;

        internal WafLibraryInvoker? WafLibraryInvoker { get; }

        internal IEncoder? Encoder { get; }

        internal IntPtr WafBuilderHandle { get; }

        internal IntPtr WafHandle { get; }

        internal ReportedDiagnostics ReportedDiagnostics { get; }

        internal string RuleFileVersion => ReportedDiagnostics.RulesetVersion;

        internal string ErrorMessage { get; } = string.Empty;

        internal bool HasRuleErrors { get; }

        internal IReadOnlyDictionary<string, object>? RuleErrors => ReportedDiagnostics.Rules.Errors;

        public static UpdateResult FromSuccess(in DdwafObjectStruct diagObj, IntPtr builderHandle, IntPtr wafHandle, WafLibraryInvoker invoker, IEncoder encoder) => new(diagObj, builderHandle, wafHandle, invoker, encoder);

        internal static UpdateResult FromFailed(string message, in DdwafObjectStruct diagObject, IntPtr builderHandle, WafLibraryInvoker? invoker, IEncoder encoder) => new(message, diagObject, builderHandle, invoker, encoder);

        internal static UpdateResult FromException(Exception e) => FromFailed(e.Message);

        internal static UpdateResult FromFailed(string message)
        {
            return new(message, default(DdwafObjectStruct));
        }
    }
}
