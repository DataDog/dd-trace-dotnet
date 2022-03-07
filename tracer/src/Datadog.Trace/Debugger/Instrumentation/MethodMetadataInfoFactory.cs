// <copyright file="MethodMetadataInfoFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Reflection;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Responsible to create <see cref="MethodMetadataInfo"/> structures.
    /// </summary>
    internal static class MethodMetadataInfoFactory
    {
        public static MethodMetadataInfo Create(MethodBase method)
        {
            var parameterNames = method.GetParameters()?.Select(parameter => parameter.Name).ToArray() ?? Array.Empty<string>();
            return new MethodMetadataInfo(parameterNames);
        }
    }
}
