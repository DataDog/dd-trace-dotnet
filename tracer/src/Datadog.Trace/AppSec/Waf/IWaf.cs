// <copyright file="IWaf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf
{
    internal interface IWaf : IDisposable
    {
        public string Version { get; }

        public IContext CreateContext();

        internal DDWAF_RET_CODE Run(IntPtr contextHandle, IntPtr rawArgs, ref DdwafResultStruct retNative, ulong timeoutMicroSeconds);

        /// <summary>
        /// only this one returns a rulsetinfo from the waf
        /// </summary>
        /// <param name="rules">json rules</param>
        /// <returns>returns InitOrUpdateResult</returns>
        UpdateResult UpdateRules(string rules);

        bool UpdateRulesData(List<RuleData> rulesData);

        bool UpdateRulesStatus(List<RuleOverride> res, List<JToken> exclusions);
    }
}
