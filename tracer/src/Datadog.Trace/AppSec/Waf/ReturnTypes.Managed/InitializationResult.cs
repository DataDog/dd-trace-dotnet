// <copyright file="InitializationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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

        public InitializationResult(IntPtr? ruleHandle, ushort failedToLoadRules, ushort loadedRules, string ruleFileVersion, IReadOnlyDictionary<string, string[]> errors, bool unusableRuleFile = false)
        {
            HasErrors = errors.Count > 0;
            Errors = errors;
            ErrorMessage = HasErrors ? JsonConvert.SerializeObject(errors) : string.Empty;
            FailedToLoadRules = failedToLoadRules;
            LoadedRules = loadedRules;
            RuleFileVersion = ruleFileVersion;
            RuleHandle = ruleHandle;
            UnusableRuleFile = unusableRuleFile;
        }

        internal IntPtr? RuleHandle { get; }

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

        internal static InitializationResult FromUnusableRuleFile() => new(null, 0, 0, string.Empty, new Dictionary<string, string[]>(), true);

        internal static InitializationResult From(DdwafRuleSetInfoStruct ddwaRuleSetInfoStruct, IntPtr? ruleHandle)
        {
            var ddwafObjectStruct = ddwaRuleSetInfoStruct.Errors;
            var errors = Decode(ddwafObjectStruct);
            var ruleFileVersion = Marshal.PtrToStringAnsi(ddwaRuleSetInfoStruct.Version);
            return new(ruleHandle, ddwaRuleSetInfoStruct.Failed, ddwaRuleSetInfoStruct.Loaded, ruleFileVersion, errors);
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
                        var array = (DdwafObjectStruct)Marshal.PtrToStructure(arrayPtr, typeof(DdwafObjectStruct));
                        var key = Marshal.PtrToStringAnsi(array.ParameterName, (int)array.ParameterNameLength);
                        var nbEntries = (int)array.NbEntries;
                        var ruleIds = new string[nbEntries];
                        for (var j = 0; j < nbEntries; j++)
                        {
                            var errorPtr = new IntPtr(array.Array.ToInt64() + (structSize * j));
                            var error = (DdwafObjectStruct)Marshal.PtrToStructure(errorPtr, typeof(DdwafObjectStruct));
                            var ruleId = Marshal.PtrToStringAnsi(error.Array);
                            ruleIds[j] = ruleId;
                        }

                        errorsDic.Add(key, ruleIds);
                    }
                }
            }

            return errorsDic;
        }
    }
}
