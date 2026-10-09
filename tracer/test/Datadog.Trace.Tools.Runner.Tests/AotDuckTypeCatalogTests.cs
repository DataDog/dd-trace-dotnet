// <copyright file="AotDuckTypeCatalogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.IO;
using System.Linq;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The duck typing catalog of the Datadog.Trace.Aot package (the library mappings that sample runs record): a renamed or
/// removed proxy of Datadog.Trace leaves a stale mapping, which a NativeAOT build would only report as incompatible.
/// </summary>
public class AotDuckTypeCatalogTests
{
    [Fact]
    public void ProxiesOfDatadogTraceExist()
    {
        var path = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "tracer", "src", "Datadog.Trace.Aot", "ducktype-maps", "datadog-trace.json");
        var mappings = JObject.Parse(File.ReadAllText(path))["mappings"]!.Children<JObject>().ToList();
        mappings.Should().NotBeEmpty();

        // In the metadata (their members use types of the libraries). Generic proxies are checked by their definition.
        var datadogTrace = ModuleDefMD.Load(typeof(Tracer).Assembly.Location);
        var missing = mappings.Where(m => m.Value<string>("proxyAssembly") == "Datadog.Trace")
                              .Select(m => m.Value<string>("proxyType")!.Split('[')[0])
                              .Distinct()
                              .Where(name => datadogTrace.Find(name, isReflectionName: true) is null)
                              .ToList();
        missing.Should().BeEmpty();
    }
}
#endif
