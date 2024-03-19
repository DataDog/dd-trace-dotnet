// <copyright file="InstrumentationRequester.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.PInvoke;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class InstrumentationRequester
    {
        internal static void Instrument(string probeId, MethodBase method)
        {
            var rejitRequest = new NativeMethodProbeDefinition(probeId, method.DeclaringType?.FullName, method.Name, targetParameterTypesFullName: null);

            DebuggerNativeMethods.InstrumentProbes(
                new[] { rejitRequest },
                Array.Empty<NativeLineProbeDefinition>(),
                Array.Empty<NativeSpanProbeDefinition>(),
                Array.Empty<NativeRemoveProbeRequest>());
        }
    }
}
