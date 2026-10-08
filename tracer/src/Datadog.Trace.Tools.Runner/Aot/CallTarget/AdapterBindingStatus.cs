// <copyright file="AdapterBindingStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

internal enum AdapterBindingStatus
{
    /// <summary>The integration method is bound: the registration provides an adapter.</summary>
    Bound,

    /// <summary>The integration has no method for this shape: the registration records a no-op, like <c>IntegrationMapper</c> returning null.</summary>
    NoMethod,

    /// <summary><c>IntegrationMapper</c> would throw: the registration records the failure, which disables the integration for the target.</summary>
    Failure,

    /// <summary>Not supported yet (duck typing proxies): nothing is registered and the handler falls back to <c>IntegrationMapper</c>.</summary>
    Deferred,
}
#endif
