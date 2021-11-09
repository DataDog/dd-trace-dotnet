// <copyright file="IAdoNetClientData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal interface IAdoNetClientData
    {
        string IntegrationName { get; }

        string AssemblyName { get; }

        string SqlCommandType { get; }

        string MinimumVersion { get; }

        string MaximumVersion { get; }

        string DataReaderType { get; }

        string DataReaderTaskType { get; }
    }
}
