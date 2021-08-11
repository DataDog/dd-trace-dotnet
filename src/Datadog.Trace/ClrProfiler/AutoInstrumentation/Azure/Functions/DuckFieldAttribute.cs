// <copyright file="DuckFieldAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    /// <summary>
    /// DuckFieldAttribute is a workaround for Azure Functions to ducktype a field.
    /// The normal attribute will throw a System.Reflection.CustomAttributeFormatException
    /// when we set the Kind property in the attribute.
    /// </summary>
    internal class DuckFieldAttribute : DuckAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckFieldAttribute"/> class.
        /// </summary>
        /// <param name="name">Name of the field</param>
        public DuckFieldAttribute(string name)
        {
            Name = name;
            Kind = DuckKind.Field;
        }
    }
}
