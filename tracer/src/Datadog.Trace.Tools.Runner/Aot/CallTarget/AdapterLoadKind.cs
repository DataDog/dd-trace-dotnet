// <copyright file="AdapterLoadKind.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

internal enum AdapterLoadKind
{
    /// <summary><c>ldarg</c>.</summary>
    Argument,

    /// <summary><c>ldarg</c> + <c>ldobj</c>: a by-reference source passed by value.</summary>
    ArgumentValue,

    /// <summary><c>ldarg.1</c>, <c>ldc.i4</c>, <c>ldelem.ref</c>: an element of the slow path arguments array.</summary>
    ArrayElement,

    /// <summary><see cref="ArrayElement"/> + <c>unbox.any</c>.</summary>
    ArrayElementUnbox,

    /// <summary><see cref="ArrayElement"/> + <c>IntegrationMapper.ConvertType&lt;T&gt;</c> (duck typing at runtime).</summary>
    ArrayElementConvert,
}
#endif
