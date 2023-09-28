// <copyright file="ToolExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.Tools.dd_dotnet
{
    /// <summary>
    /// This class is just there to expose the ExporterSettings to the integration tests project,
    /// without causing conflicts with the ExporterSettings from the tracer project.
    /// </summary>
    internal class ToolExporterSettings : ExporterSettings
    {
        public ToolExporterSettings(IConfigurationSource? configuration)
            : base(configuration)
        {
        }

        public ToolExporterSettings(string agentUri)
            : base(agentUri)
        {
        }
    }
}
