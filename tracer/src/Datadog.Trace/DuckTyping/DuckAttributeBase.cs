// <copyright file="DuckAttributeBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Reflection;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = false)]
    public abstract class DuckAttributeBase : Attribute
    {
        /// <summary>
        /// Gets or sets the underlying type member name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets duck kind
        /// </summary>
        public DuckKind Kind { get; set; } = DuckKind.Property;

        /// <summary>
        /// Gets or sets the binding flags
        /// </summary>
        public BindingFlags BindingFlags { get; set; } = DuckAttribute.DefaultFlags;

        /// <summary>
        /// Gets or sets the generic parameter type names definition for a generic method call (required when calling generic methods and instance type is non public)
        /// </summary>
        public string[] GenericParameterTypeNames { get; set; }

        /// <summary>
        /// Gets or sets the parameter type names of the target method (optional / used to disambiguation)
        /// </summary>
        public string[] ParameterTypeNames { get; set; }

        /// <summary>
        /// Gets or sets the explicit interface type name
        /// </summary>
        public string ExplicitInterfaceTypeName { get; set; }
    }
}
