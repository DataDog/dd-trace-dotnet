// <copyright file="TypeExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util;

internal static class TypeExtensions
{
    public static bool IsAnonymous(this TypeInfo type)
    {
        return type.GetCustomAttribute<CompilerGeneratedAttribute>() != null
            && type.IsGenericType && type.Name.Contains("AnonymousType")
            && (type.Name.StartsWith("<>") || type.Name.StartsWith("VB$"))
            && (type.Attributes & TypeAttributes.Public) != TypeAttributes.Public;
    }
}
