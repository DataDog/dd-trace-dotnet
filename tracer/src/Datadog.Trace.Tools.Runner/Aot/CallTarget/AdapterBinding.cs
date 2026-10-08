// <copyright file="AdapterBinding.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.Collections.Generic;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// The result of binding one integration method to one handler shape (B2): what <c>IntegrationMapper</c> would do.
/// </summary>
internal sealed class AdapterBinding
{
    private AdapterBinding(AdapterBindingStatus status, string? message)
    {
        Status = status;
        Message = message;
    }

    public AdapterBindingStatus Status { get; }

    public string? Message { get; }

    public MethodDef? Method { get; private set; }

    public List<TypeSig> GenericArguments { get; } = new();

    public List<AdapterLoad> Loads { get; } = new();

    public List<RuntimeTypeCheck> Checks { get; } = new();

    public bool PreserveContext { get; private set; }

    /// <summary>Gets a value indicating whether <c>OnAsyncMethodEnd</c> returns a task (async continuation).</summary>
    public bool IsTaskReturn { get; private set; }

    /// <summary>Gets the proxy of the return value, which the adapter unwraps after the call (<c>IntegrationMapper.UnwrapReturnValue</c>).</summary>
    public DuckProxy? ReturnProxy { get; private set; }

    /// <summary>Gets the type the return value proxy unwraps to.</summary>
    public TypeSig? ReturnType { get; private set; }

    /// <summary>Gets the indexes of the generic arguments bound to proxies, whose constraints hold by construction.</summary>
    public HashSet<int> ProxyArguments { get; } = new();

    public static AdapterBinding NoMethod() => new(AdapterBindingStatus.NoMethod, null);

    public static AdapterBinding Failure(string message) => new(AdapterBindingStatus.Failure, message);

    public static AdapterBinding Deferred(string message) => new(AdapterBindingStatus.Deferred, message);

    public static AdapterBinding Bound(MethodDef method, bool preserveContext = false, bool isTaskReturn = false)
        => new(AdapterBindingStatus.Bound, null) { Method = method, PreserveContext = preserveContext, IsTaskReturn = isTaskReturn };

    public void SetReturnProxy(DuckProxy proxy, TypeSig returnType)
    {
        ReturnProxy = proxy;
        ReturnType = returnType;
    }
}
#endif
