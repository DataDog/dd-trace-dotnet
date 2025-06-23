// <copyright file="ExporterSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using Datadog.Trace.Tools.dd_dotnet;

// ReSharper disable once CheckNamespace
// Disabled to match the partial file
namespace Datadog.Trace.Tests.Configuration;

extern alias tracer;

using System;
using Datadog.Trace.Configuration;

/// <summary>
/// Null implementations for things that are not used in the dd-dotnet tool.
/// </summary>
public partial class ExporterSettingsTests
{
    private const string DefaultMetricsUnixDomainSocket = "/var/run/datadog/dsd.socket";

    private void AssertMetricsUdpIsConfigured(ExporterSettings settings, string hostname = "", int port = 0)
    {
    }

    private Func<string, bool> DefaultSocketFilesExist()
    {
        return FileExistsMock(ExporterSettings.DefaultTracesUnixDomainSocket, DefaultMetricsUnixDomainSocket);
    }

    private void CheckSpecificDefaultValues(ExporterSettings settings, string[] paramToIgnore)
    {
    }

    private ExporterSettings Setup(IConfigurationSource source, Func<string, bool> fileExists)
    {
        return new ExporterSettings(source, fileExists);
    }

    internal class NameValueConfigurationSource : IConfigurationSource
    {
        private readonly NameValueCollection _collection;

        public NameValueConfigurationSource(NameValueCollection collection)
        {
            _collection = collection;
        }

        public string GetString(string key) => _collection[key];
    }
}
