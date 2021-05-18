// <copyright file="ResultMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Crank.Models
{
    internal class ResultMetadata
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string Format { get; set; }
    }
}
