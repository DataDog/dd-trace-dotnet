// <copyright file="ProbeDefinition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Configurations.Models
{
    internal abstract class ProbeDefinition : IJsonApiObject
    {
        public string Language { get; set; }

        public string Id { get; set; }

        public long? OrgId { get; set; }

        public string AppId { get; set; }

        public DateTime Created { get; set; }

        public DateTime? Updated { get; set; }

        public bool Active { get; set; }

        public Tag[] Tags { get; set; }

        public Where Where { get; set; }

        public string[] AdditionalIds { get; set; }

        public int? Version { get; set; }
    }
}
