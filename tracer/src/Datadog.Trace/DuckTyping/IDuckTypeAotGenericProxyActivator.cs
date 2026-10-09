// <copyright file="IDuckTypeAotGenericProxyActivator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.DuckTyping;

/// <summary>
/// Creates the proxies of a generated generic proxy type for one instantiation of an open generic target type (see
/// <see cref="DuckType.RegisterAotGenericProxy"/>): the registry generates it generic over the type parameters of the target,
/// and the engine instantiates it with the type arguments of the runtime type.
/// </summary>
internal interface IDuckTypeAotGenericProxyActivator
{
    /// <summary>
    /// Creates the proxy of an instance.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="targetType">The runtime type the proxy reports as IDuckType.Type.</param>
    /// <returns>The proxy.</returns>
    object? CreateInstance(object? instance, Type targetType);

    /// <summary>
    /// Gets the proxy type of the instantiation.
    /// </summary>
    /// <returns>The proxy type.</returns>
    Type GetProxyType();
}
