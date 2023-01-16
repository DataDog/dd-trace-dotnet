// <copyright file="IWaf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal interface IWaf : IDisposable
    {
        public string Version { get; }

        public Encoder Encoder { get; }

        public IContext CreateContext(Security security);

        public bool UpdateRulesData(IEnumerable<RuleData> res);

        public bool ToggleRules(IDictionary<string, bool> ruleStatus);

        internal DDWAF_RET_CODE Run(IntPtr contextHandle, IntPtr rawArgs, ref DdwafResultStruct retNative, ulong timeoutMicroSeconds);

        internal void ResultFree(ref DdwafResultStruct retNative);

        internal void ContextDestroy(IntPtr contextHandle);
    }
}
