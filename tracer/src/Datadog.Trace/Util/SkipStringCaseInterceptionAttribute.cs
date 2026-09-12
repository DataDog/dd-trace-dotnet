// <copyright file="SkipStringCaseInterceptionAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Util;

/// <summary>
/// Opts a method, or every method in a type, out of the StringCaseInterception source generator - the
/// generator that, on .NET Framework only, rewrites <see cref="string.ToUpperInvariant()"/>/
/// <see cref="string.ToLowerInvariant()"/> call sites in this compilation to call <see cref="StringUtil"/>
/// instead, avoiding the allocation those methods otherwise always incur on that TFM.
/// </summary>
/// <remarks>
/// Known gap: applying this to a method does not cover call sites inside a lambda or local function
/// declared within that method - the generator resolves the enclosing symbol for those to the lambda/local
/// function itself, not the attributed outer method.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
internal sealed class SkipStringCaseInterceptionAttribute : Attribute
{
}
