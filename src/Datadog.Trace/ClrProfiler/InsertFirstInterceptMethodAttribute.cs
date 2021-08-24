// <copyright file="InsertFirstInterceptMethodAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Attribute that indicates that the decorated method is meant to intercept calls
    /// to another method. Used to generate the integration definitions file.
    /// </summary>
    internal class InsertFirstInterceptMethodAttribute : InterceptMethodAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InsertFirstInterceptMethodAttribute"/> class.
        /// </summary>
        public InsertFirstInterceptMethodAttribute()
        {
            MethodReplacementAction = MethodReplacementActionType.InsertFirst;
        }
    }
}
