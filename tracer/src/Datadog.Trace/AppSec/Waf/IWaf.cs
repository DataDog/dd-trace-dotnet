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

        /// <summary>
        /// Evaluates persistent data, whose side effects live for the whole context.
        /// </summary>
        internal unsafe WafReturnCode ContextEval(IntPtr contextHandle, DdwafObjectStruct* rawData, ref DdwafObjectStruct retNative, ulong timeoutMicroSeconds);

        /// <summary>
        /// Creates a subcontext, whose side effects are discarded when it is destroyed. This replaces
        /// the ephemeral data of libddwaf 1.x.
        /// </summary>
        internal IntPtr SubcontextInit(IntPtr contextHandle);

        /// <summary>
        /// Evaluates data within a subcontext, so that its side effects don't leak into the context.
        /// </summary>
        internal unsafe WafReturnCode SubcontextEval(IntPtr subcontextHandle, DdwafObjectStruct* rawData, ref DdwafObjectStruct retNative, ulong timeoutMicroSeconds);

        UpdateResult Update(ConfigurationState configurationStatus);

        public string[] GetKnownAddresses();

        public bool IsKnowAddressesSuported();
    }
}
