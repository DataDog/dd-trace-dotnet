// <copyright file="DynamicMethodExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.ExtensionMethods;

internal static class DynamicMethodExtensions
{
    private static readonly MethodInfo GetMethodDescriptor;

    static DynamicMethodExtensions()
    {
        GetMethodDescriptor = typeof(DynamicMethod)?
           .GetMethod("GetMethodDescriptor", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    public static IntPtr GetFunctionPointer(this DynamicMethod dynMethod)
    {
        return ((RuntimeMethodHandle)GetMethodDescriptor.Invoke(dynMethod, null)).GetFunctionPointer();
    }
}
