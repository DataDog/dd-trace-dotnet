// <copyright file="FeatureTrackingAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Ci
{
    /// <summary>
    /// Expose a constant as a feature tracking value
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    internal class FeatureTrackingAttribute : Attribute
    {
    }
}
