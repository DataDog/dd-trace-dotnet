// <copyright file="DuckReverseMethodAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck reverse method attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class DuckReverseMethodAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckReverseMethodAttribute"/> class.
        /// </summary>
        public DuckReverseMethodAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DuckReverseMethodAttribute"/> class.
        /// </summary>
        /// <param name="arguments">Methods arguments</param>
        public DuckReverseMethodAttribute(params string[] arguments)
        {
            Arguments = arguments;
        }

        /// <summary>
        /// Gets the methods arguments
        /// </summary>
        public string[] Arguments { get; private set; }
    }
}
