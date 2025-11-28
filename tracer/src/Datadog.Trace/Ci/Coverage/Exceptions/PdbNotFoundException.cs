// <copyright file="PdbNotFoundException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Ci.Coverage.Exceptions;

/// <summary>
/// Pdb not found
/// </summary>
internal sealed class PdbNotFoundException : Exception
{
    /// <summary>
    /// Throw the exception
    /// </summary>
    /// <exception cref="PdbNotFoundException">Throws current exception</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    public static void Throw()
    {
        throw new PdbNotFoundException();
    }
}
