// <copyright file="MD5HashProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Debugger.Helpers;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal static class MD5HashProvider
    {
        internal static string GetHash(ExceptionIdentifier exceptionId)
        {
            return ((byte)exceptionId.ErrorOrigin + string.Concat(exceptionId.ExceptionTypes.Select(ex => ex.FullName)) + string.Concat(exceptionId.StackTrace.Select(method => method.Method.GetFullyQualifiedName() ?? method.Method.Name))).ToUUID();
        }
    }
}
