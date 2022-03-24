// <copyright file="TraceAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TraceAttribute : Attribute
    {
        public string OperationName { get; set; }

        public string ResourceName { get; set; }
    }
}
