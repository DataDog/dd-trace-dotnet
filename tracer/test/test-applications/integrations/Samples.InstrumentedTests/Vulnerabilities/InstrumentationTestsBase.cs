// <copyright file="InstrumentationTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace;
using FluentAssertions;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class InstrumentationTestsBase
{
    private TraceContext traceContext;

    public InstrumentationTestsBase()
    {
        Tracer.Instance.StartActiveInternal("test", null, DateTime.Now.ToString());
        traceContext = (Tracer.Instance.ActiveScope.Span as Span).Context.TraceContext;
        AssertInstrumented();
        traceContext.EnableIastInRequest();
    }

    protected void AddTainted(string tainted)
    {
        traceContext.IastRequestContext.AddTaintedForTest(null, tainted);
    }

    protected void AssertTainted(string tainted)
    {
        traceContext.IastRequestContext.GetTainted(tainted).Should().NotBeNull();
    }

    protected void AssertNotTainted(string value)
    {
        traceContext.IastRequestContext.GetTainted(value).Should().BeNull();
    }

    protected void AssertInstrumented()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var loaderAssembliesCount = assemblies.Where(x => x.GetName().Name == "Datadog.Trace.ClrProfiler.Managed.Loader").Count();
        loaderAssembliesCount.Should().NotBe(0, GetErrorMessage(assemblies));
        traceContext.Should().NotBeNull(GetErrorMessage(assemblies));
    }

    protected void AssertSpanGenerated(string operationName, int spansGenerated = 1)
    {
        var spans = GetGeneratedSpans(traceContext);
        spans = spans.Where(x => x.OperationName == operationName).ToList();
        spansGenerated.Should().Be(spans.Count);
    }

    protected void AssertVulnerable(int vulnerabilities = 1)
    {
        var spans = GetGeneratedSpans(traceContext);
        GetIastSpansCount(spans).Should().Be(vulnerabilities);
    }

    protected void AssertNotVulnerable()
    {
        AssertVulnerable(0);
    }

    private static string GetErrorMessage(Assembly[] assemblies)
    {
        var assemblyListString = "DD Assemblies:" + Environment.NewLine  + string.Join(Environment.NewLine, assemblies.Where(x => x.GetName().Name.IndexOf("datadog", StringComparison.OrdinalIgnoreCase) >= 0).Select(x => x.GetName().Name));

        string homeFiles = "COR files:" + Environment.NewLine;
#if NETCOREAPP
        homeFiles += string.Join("; ", Directory.GetFiles(System.IO.Path.GetDirectoryName(EnvironmentVariableMessage("CORECLR_PROFILER_PATH"))));
#else
        homeFiles += string.Join("; ", Directory.GetFiles(System.IO.Path.GetDirectoryName(EnvironmentVariableMessage("COR_PROFILER_PATH"))));
#endif

        return "Test is not instrumented." + Environment.NewLine +
            EnvironmentVariableMessage("CORECLR_ENABLE_PROFILING") +
            EnvironmentVariableMessage("CORECLR_PROFILER_PATH") + EnvironmentVariableMessage("CORECLR_PROFILER_PATH_64") + EnvironmentVariableMessage("CORECLR_PROFILER_PATH_32") +
            EnvironmentVariableMessage("COR_ENABLE_PROFILING") + EnvironmentVariableMessage("COR_PROFILER_PATH_32") +
            EnvironmentVariableMessage("COR_PROFILER_PATH") +
            EnvironmentVariableMessage("COR_PROFILER_PATH_64") + EnvironmentVariableMessage("DD_DOTNET_TRACER_HOME") + Environment.NewLine +
            assemblyListString + Environment.NewLine + homeFiles;
    }

    private static string EnvironmentVariableMessage(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return variable + ": " +  (string.IsNullOrEmpty(value) ? "Empty" : value) + Environment.NewLine;
    }

    private int GetIastSpansCount(List<Span> spans)
    {
        return spans.Where(x => x.GetTag(Tags.IastEnabled) != null).Count();
    }

    private List<Span> GetGeneratedSpans(TraceContext context)
    {
        var spans = new List<Span>();
        var contextSpans = context.Spans.GetArray();

        foreach (var span in contextSpans)
        {
            spans.Add(span);
        }

        return spans;
    }
}
