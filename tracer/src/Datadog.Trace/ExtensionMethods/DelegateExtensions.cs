// <copyright file="DelegateExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;

namespace Datadog.Trace.ExtensionMethods;

internal static class DelegateExtensions
{
    private static readonly object DummyInstanceObject = new();

    public static Delegate CreateInstanceDelegate(this MethodInfo methodInfo, Type delegateType)
    {
        return methodInfo.CreateDelegate(delegateType, DummyInstanceObject);
    }

    public static T CreateInstanceDelegate<T>(this MethodInfo methodInfo)
        where T : Delegate
    {
        return (T)CreateInstanceDelegate(methodInfo, typeof(T));
    }
}
