// <copyright file="IWaf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf
{
    internal interface IWaf : IDisposable
    {
        public string Version { get; }

        public IContext CreateContext();

        internal WafReturnCode Run(IntPtr contextHandle, ref DdwafObjectStruct persistentData, ref DdwafObjectStruct ephemeralData, ref DdwafResultStruct retNative, ulong timeoutMicroSeconds);

        UpdateResult UpdateWafFromConfigurationStatus(ConfigurationStatus configurationStatus);
    }
}
