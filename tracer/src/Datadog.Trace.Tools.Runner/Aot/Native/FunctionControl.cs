// <copyright file="FunctionControl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

internal sealed unsafe class FunctionControl : ICorProfilerFunctionControl
{
    private readonly EmulatedRuntime _runtime;
    private readonly int _module;
    private readonly uint _methodToken;

    public FunctionControl(EmulatedRuntime runtime, int module, uint methodToken)
    {
        _runtime = runtime;
        _module = module;
        _methodToken = methodToken;
    }

    public HResult QueryInterface(in Guid guid, out IntPtr ptr)
    {
        ptr = IntPtr.Zero;
        return HResult.E_NOINTERFACE;
    }

    public int AddRef() => 1;

    public int Release() => 1;

    public HResult SetCodegenFlags(int flags) => HResult.S_OK;

    public HResult SetILFunctionBody(uint cbNewILMethodHeader, IntPtr pbNewILMethodHeader)
    {
        _runtime.StoreNewBody(_module, _methodToken, pbNewILMethodHeader, (int)cbNewILMethodHeader);
        return HResult.S_OK;
    }

    public HResult SetILInstrumentedCodeMap(uint cILMapEntries, CorIlMap* rgILMapEntries) => HResult.S_OK;
}
#endif
