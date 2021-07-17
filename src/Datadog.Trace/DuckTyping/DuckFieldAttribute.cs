// <copyright file="DuckFieldAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck attribute where the underlying member is a field
    /// </summary>
    public class DuckFieldAttribute : DuckAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckFieldAttribute"/> class.
        /// </summary>
        public DuckFieldAttribute()
        {
            Kind = DuckKind.Field;
        }
    }
}
