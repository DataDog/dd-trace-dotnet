// <copyright file="IntegrationAreaAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    /// <summary>
    /// Optional attribute to mark a test class with the integration areas it is testing to help
    /// dynamically choose whether to run a full or reduced test suite based on changed files in PRs.
    /// Supports multiple areas - use <see cref="KnownTestAreas"/>.
    /// </summary>
    /// <param name="areas">The <see cref="KnownTestAreas"/> that this test touches.</param>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class IntegrationAreaAttribute(params string[] areas) : Attribute
    {
        public string[] Areas { get; } = areas;
    }
}
