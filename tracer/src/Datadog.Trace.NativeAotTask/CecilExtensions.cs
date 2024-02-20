// <copyright file="CecilExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;

namespace Datadog.Trace.NativeAotTask;

internal static class CecilExtensions
{
    public static MethodReference MakeGenericMethod(this MethodReference method, TypeReference genericDeclaringType)
    {
        var genericReference = new MethodReference(method.Name, method.ReturnType, genericDeclaringType)
        {
            CallingConvention = method.CallingConvention,
            HasThis = method.HasThis,
            ExplicitThis = method.ExplicitThis,
        };

        foreach (var parameter in method.Parameters)
        {
            genericReference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
        }

        return genericReference;
    }
}
