// <copyright file="ICorProfilerCallback9.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerCallback9 : ICorProfilerCallback8
{
    public static new readonly Guid Guid = Guid.Parse("27583EC3-C8F5-482F-8052-194B8CE4705A");

    // This event is triggered whenever a dynamic method is garbage collected
    // and subsequently unloaded.
    HResult DynamicMethodUnloaded(FunctionId functionId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback9.DynamicMethodUnloaded");
        return HResult.E_NOTIMPL;
    }
}
#endif
