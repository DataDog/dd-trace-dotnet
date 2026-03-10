// <copyright file="DuckAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Reflection;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck kind
    /// </summary>
    internal enum DuckKind
    {
        /// <summary>
        /// The target member is a Property
        /// </summary>
        Property,

        /// <summary>
        /// The target member is a Field
        /// </summary>
        Field,

        /// <summary>
        /// The target member could be a Property or a Field.
        /// Both members will be checked for, the first matching member will be used.
        /// Property members are checked for first.
        /// </summary>
        PropertyOrField
    }

    /// <summary>
    /// Duck attribute
    /// </summary>
    internal class DuckAttribute : DuckAttributeBase
    {
        /// <summary>
        /// Default BindingFlags
        /// </summary>
        public const BindingFlags DefaultFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        /// <summary>
        /// Gets or sets duck kind
        /// </summary>
        public DuckKind Kind { get; set; } = DuckKind.Property;

        /// <summary>
        /// Gets or sets a value indicating whether we need look up the base types in case the member is not found in the existing type
        /// </summary>
        public bool FallbackToBaseTypes { get; set; } = false;
    }
}
