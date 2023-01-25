// <copyright file="InitializationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypesManaged
{
    internal class InitializationResult
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InitializationResult));

        public InitializationResult(ushort failedToLoadRules, ushort loadedRules, string ruleFileVersion, IReadOnlyDictionary<string, string[]> errors, bool unusableRuleFile = false, IntPtr? wafHandle = null, WafLibraryInvoker? wafLibraryInvoker = null)
        {
            HasErrors = errors.Count > 0;
            Errors = errors;
            FailedToLoadRules = failedToLoadRules;
            LoadedRules = loadedRules;
            RuleFileVersion = ruleFileVersion;
            UnusableRuleFile = unusableRuleFile;
            ErrorMessage = string.Empty;
            if (HasErrors)
            {
                ErrorMessage = JsonConvert.SerializeObject(errors);
            }

            if (!unusableRuleFile && wafHandle.HasValue && wafHandle.Value != IntPtr.Zero)
            {
                Waf = new Waf(wafHandle.Value, wafLibraryInvoker!);
                Success = true;
            }
        }

        internal bool Success { get; }

        internal Waf? Waf { get; }

        internal ushort FailedToLoadRules { get; }

        /// <summary>
        /// Gets the number of rules successfully loaded
        /// </summary>
        internal ushort LoadedRules { get; }

        internal IReadOnlyDictionary<string, string[]> Errors { get; }

        internal string ErrorMessage { get; }

        internal bool HasErrors { get; }

        internal bool UnusableRuleFile { get; }

        internal string RuleFileVersion { get; }

        internal bool Reported { get; set; }

        internal static InitializationResult FromUnusableRuleFile() => new(0, 0, string.Empty, new Dictionary<string, string[]>(), unusableRuleFile: true);

        internal static InitializationResult From(DdwafRuleSetInfoStruct ddwaRuleSetInfoStruct, IntPtr? wafHandle, WafLibraryInvoker wafLibraryInvoker)
        {
            var ddwafObjectStruct = ddwaRuleSetInfoStruct.Errors;
            var errors = Decode(ddwafObjectStruct);
            var ruleFileVersion = Marshal.PtrToStringAnsi(ddwaRuleSetInfoStruct.Version);
            return new(ddwaRuleSetInfoStruct.Failed, ddwaRuleSetInfoStruct.Loaded, ruleFileVersion!, errors, wafHandle: wafHandle, wafLibraryInvoker: wafLibraryInvoker);
        }

        private static IReadOnlyDictionary<string, string[]> Decode(DdwafObjectStruct ddwafObjectStruct)
        {
            var nbEntriesStart = (int)ddwafObjectStruct.NbEntries;
            var errorsDic = new Dictionary<string, string[]>(nbEntriesStart);
            if (nbEntriesStart > 0)
            {
                if (ddwafObjectStruct.Type != DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP)
                {
                    Log.Warning("Expecting type {DDWAF_OBJ_MAP} to decode waf errors and instead got a {Type} ", nameof(DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP), ddwafObjectStruct.Type);
                }
                else
                {
                    var structSize = Marshal.SizeOf(typeof(DdwafObjectStruct));
                    for (var i = 0; i < nbEntriesStart; i++)
                    {
                        var arrayPtr = new IntPtr(ddwafObjectStruct.Array.ToInt64() + (structSize * i));
                        var array = (DdwafObjectStruct?)Marshal.PtrToStructure(arrayPtr, typeof(DdwafObjectStruct));
                        if (array is { } arrayValue)
                        {
                            var key = Marshal.PtrToStringAnsi(arrayValue.ParameterName, (int)arrayValue.ParameterNameLength);
                            var nbEntries = (int)arrayValue.NbEntries;
                            var ruleIds = new string[nbEntries];
                            for (var j = 0; j < nbEntries; j++)
                            {
                                var errorPtr = new IntPtr(arrayValue.Array.ToInt64() + (structSize * j));
                                var error = (DdwafObjectStruct?)Marshal.PtrToStructure(errorPtr, typeof(DdwafObjectStruct));
                                var ruleId = Marshal.PtrToStringAnsi(error!.Value.Array);
                                ruleIds[j] = ruleId!;
                            }

                            errorsDic.Add(key, ruleIds);
                        }
                    }
                }
            }

            return errorsDic;
        }
    }
}
