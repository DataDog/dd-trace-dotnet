// <copyright file="TracerConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    /// <summary>
    /// The version members of this class are generated from version.props into the intermediate
    /// output directory - see tracer/src/Directory.Build.targets. They used to be committed
    /// literals that the version-bump tooling rewrote with a regex.
    /// </summary>
    internal static partial class TracerConstants
    {
        public const string TelemetrySdkName = "datadog";
        public const string Language = "dotnet";
    }
}
