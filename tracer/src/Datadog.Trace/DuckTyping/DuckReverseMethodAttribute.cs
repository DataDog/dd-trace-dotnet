// <copyright file="DuckReverseMethodAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Marks a member inside a reverse duck-typing contract for reverse method binding metadata.
    /// </summary>
    /// <remarks>
    /// This member-level attribute is independent from <see cref="DuckReverseAttribute"/>, which declares
    /// type-level reverse target mapping metadata used by AOT discovery.
    /// </remarks>
    internal sealed class DuckReverseMethodAttribute : DuckAttributeBase
    {
    }
}
