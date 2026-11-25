// <copyright file="MemberInfoExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// ReSharper disable once CheckNamespace - In System.Reflection so it sits next to CustomAttributeExtensions.GetCustomAttribute<T>()
namespace System.Reflection;

internal static class MemberInfoExtensions
{
    /// <summary>
    /// Indicates whether an attribute of type <typeparamref name="T"/> is applied to <paramref name="element"/>,
    /// including inherited attributes. Unlike <c>GetCustomAttribute&lt;T&gt;() is not null</c>, this doesn't create
    /// attribute instances, and it looks up inherited attributes the same way, including on properties and events.
    /// </summary>
    /// <param name="element">The member to inspect</param>
    /// <typeparam name="T">The attribute type to look for</typeparam>
    /// <returns>true if an attribute of type <typeparamref name="T"/> is applied to <paramref name="element"/>; otherwise, false.</returns>
    public static bool HasCustomAttribute<T>(this MemberInfo element)
        where T : Attribute
    {
#pragma warning disable RS0030 // Do not use banned APIs - this is the wrapper the ban points to
        return Attribute.IsDefined(element, typeof(T));
#pragma warning restore RS0030
    }
}
