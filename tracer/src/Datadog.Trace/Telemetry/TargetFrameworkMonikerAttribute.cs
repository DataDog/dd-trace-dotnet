// <copyright file="TargetFrameworkMonikerAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Telemetry
{
    /// <summary>
    /// Marks class with TargetFrameworkMoniker field
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    [AttributeUsage(AttributeTargets.Class)]
    internal class TargetFrameworkMonikerAttribute : Attribute
    {
        public TargetFrameworkMonikerAttribute(string tfm)
        {
            TargetFrameworkMoniker = tfm;
        }

        public string TargetFrameworkMoniker { get; }
    }
}
