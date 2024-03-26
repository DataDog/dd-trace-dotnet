// <copyright file="ServiceConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.Configurations.Models
{
    internal class ServiceConfiguration
    {
        public string? Id { get; set; }

        public FilterList? AllowList { get; set; }

        public FilterList? DenyList { get; set; }

        public Sampling? Sampling { get; set; }

        public OpsConfiguration? OpsConfiguration { get; set; }
    }
}
