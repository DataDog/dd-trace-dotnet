// <copyright file="InitializationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypesManaged
{
    internal class InitializationResult
    {
        public InitializationResult(IntPtr? ruleHandle, ushort failedToLoadRules, ushort loadedRules, string ruleFileVersion, string errors)
        {
            Errors = errors;
            FailedToLoadRules = failedToLoadRules;
            LoadedRules = loadedRules;
            RuleFileVersion = ruleFileVersion;
            RuleHandle = ruleHandle;
        }

        internal IntPtr? RuleHandle { get; }

        internal ushort FailedToLoadRules { get; }

        /// <summary>
        /// Gets the number of rules successfully loaded
        /// </summary>
        internal ushort LoadedRules { get; }

        internal string Errors { get; }

        internal string RuleFileVersion { get; }

        internal static InitializationResult From(WafNative wafNative, DdwafRuleSetInfoStruct ddwaRuleSetInfoStruct, IntPtr? ruleHandle)
        {
            var errors = string.Empty;
            var ddwafObjectStruct = (DdwafObjectStruct)ddwaRuleSetInfoStruct.Errors;
            if (ddwafObjectStruct.NbEntries > 0)
            {
                var res = Decode(wafNative, ddwafObjectStruct);
                errors = JsonConvert.SerializeObject(res);
            }

            return new(ruleHandle, ddwaRuleSetInfoStruct.Failed, ddwaRuleSetInfoStruct.Loaded, ddwaRuleSetInfoStruct.Version, errors);
        }

        private static IDictionary<string, object> Decode(WafNative wafNative, DdwafObjectStruct ddwafObjectStruct)
        {
            var errorsDic = new Dictionary<string, object>();
            var structSize = Marshal.SizeOf(typeof(DdwafObjectStruct));
            for (var i = 0; i < (int)ddwafObjectStruct.NbEntries; i++)
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
                    var ruleId = Marshal.PtrToStringAnsi(error.StringValue);
                    ruleIds[j] = ruleId;
                }

                errorsDic.Add(key, ruleIds);
            }

            return errorsDic;
        }
    }
}
