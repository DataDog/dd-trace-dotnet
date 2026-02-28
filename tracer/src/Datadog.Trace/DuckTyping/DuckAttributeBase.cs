// <copyright file="DuckAttributeBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = false)]
    internal abstract class DuckAttributeBase : Attribute
    {
        /// <summary>
        /// Gets or sets name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets binding flags.
        /// </summary>
        public BindingFlags BindingFlags { get; set; } = DuckAttribute.DefaultFlags;

        /// <summary>
        /// Gets or sets generic parameter type names.
        /// </summary>
        public string[]? GenericParameterTypeNames { get; set; }

        /// <summary>
        /// Gets or sets parameter type names.
        /// </summary>
        public string[]? ParameterTypeNames { get; set; }

        /// <summary>
        /// Gets or sets explicit interface type name.
        /// </summary>
        public string? ExplicitInterfaceTypeName { get; set; }
    }
}
