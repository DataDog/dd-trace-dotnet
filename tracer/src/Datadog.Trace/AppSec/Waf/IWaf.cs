// <copyright file="IWaf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;

namespace Datadog.Trace.AppSec.Waf
{
    internal interface IWaf : IDisposable
    {
        public string Version { get; }

        bool Disposed { get; }

        public IContext? CreateContext();

        internal unsafe WafReturnCode Run(IntPtr contextHandle, DdwafObjectStruct* rawPersistentData, DdwafObjectStruct* rawEphemeralData, ref DdwafObjectStruct retNative, ulong timeoutMicroSeconds);

        UpdateResult Update(ConfigurationState configurationStatus);

        public string[] GetKnownAddresses();

        public bool IsKnowAddressesSuported();
    }
}
