// <copyright file="ProbeConfigurationFileLoaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class ProbeConfigurationFileLoaderTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task LoadAsync_WithoutProbeFile_ReturnsNull(string? probeFile)
    {
        var configuration = await ProbeConfigurationFileLoader.LoadAsync(probeFile);

        configuration.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsNull()
    {
        var configuration = await ProbeConfigurationFileLoader.LoadAsync(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        configuration.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    [InlineData("[")]
    [InlineData("[]")]
    public async Task LoadAsync_InvalidOrEmptyFile_ReturnsNull(string content)
    {
        var configuration = await ProbeConfigurationFileLoader.LoadAsync(CreateTempProbeFile(content));

        configuration.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_MultipleProbeTypes_LoadsAllTypes()
    {
        var configuration = await ProbeConfigurationFileLoader.LoadAsync(
            CreateTempProbeFile(
                """
                [
                  {
                    "id": "log-1",
                    "language": "dotnet",
                    "type": "LOG_PROBE",
                    "where": { "sourceFile": "log.cs", "lines": ["10"] },
                    "template": "Hello",
                    "captureSnapshot": true
                  },
                  {
                    "id": "metric-1",
                    "language": "dotnet",
                    "type": "METRIC_PROBE",
                    "where": { "typeName": "MyClass", "methodName": "MyMethod" },
                    "kind": "COUNT",
                    "metricName": "my.metric"
                  },
                  {
                    "id": "span-1",
                    "language": "dotnet",
                    "type": "SPAN_PROBE",
                    "where": { "typeName": "MyClass", "methodName": "MyMethod" }
                  },
                  {
                    "id": "span-decoration-1",
                    "language": "dotnet",
                    "type": "SPAN_DECORATION_PROBE",
                    "where": { "typeName": "MyClass", "methodName": "MyMethod" },
                    "targetSpan": "ACTIVE",
                    "decorations": []
                  }
                ]
                """));

        configuration.Should().NotBeNull();
        configuration!.LogProbes.Should().ContainSingle().Which.Id.Should().Be("log-1");
        configuration.MetricProbes.Should().ContainSingle().Which.Id.Should().Be("metric-1");
        configuration.SpanProbes.Should().ContainSingle().Which.Id.Should().Be("span-1");
        configuration.SpanDecorationProbes.Should().ContainSingle().Which.Id.Should().Be("span-decoration-1");
    }

    [Fact]
    public async Task LoadAsync_InvalidEntries_AreSkipped()
    {
        var configuration = await ProbeConfigurationFileLoader.LoadAsync(
            CreateTempProbeFile(
                """
                [
                  1,
                  { "id": "missing-type", "language": "dotnet" },
                  { "id": "unknown-type", "language": "dotnet", "type": "UNKNOWN_PROBE" },
                  {
                    "language": "dotnet",
                    "type": "LOG_PROBE",
                    "where": { "sourceFile": "missing-id.cs", "lines": ["10"] }
                  },
                  {
                    "id": "valid-log",
                    "language": "dotnet",
                    "type": "LOG_PROBE",
                    "where": { "sourceFile": "valid.cs", "lines": ["10"] }
                  }
                ]
                """));

        configuration.Should().NotBeNull();
        configuration!.LogProbes.Should().ContainSingle().Which.Id.Should().Be("valid-log");
        configuration.MetricProbes.Should().BeEmpty();
        configuration.SpanProbes.Should().BeEmpty();
        configuration.SpanDecorationProbes.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_DuplicateProbeIds_KeepFirstOccurrence()
    {
        var configuration = await ProbeConfigurationFileLoader.LoadAsync(
            CreateTempProbeFile(
                """
                [
                  {
                    "id": "duplicate-id",
                    "language": "dotnet",
                    "type": "LOG_PROBE",
                    "where": { "sourceFile": "first.cs", "lines": ["10"] },
                    "template": "First"
                  },
                  {
                    "id": "duplicate-id",
                    "language": "dotnet",
                    "type": "LOG_PROBE",
                    "where": { "sourceFile": "second.cs", "lines": ["20"] },
                    "template": "Second"
                  },
                  {
                    "id": "unique-id",
                    "language": "dotnet",
                    "type": "LOG_PROBE",
                    "where": { "sourceFile": "unique.cs", "lines": ["30"] },
                    "template": "Unique"
                  }
                ]
                """));

        configuration.Should().NotBeNull();
        configuration!.LogProbes.Should().HaveCount(2);
        configuration.LogProbes[0].Id.Should().Be("duplicate-id");
        configuration.LogProbes[0].Template.Should().Be("First");
        configuration.LogProbes[0].Where.SourceFile.Should().Be("first.cs");
        configuration.LogProbes[1].Id.Should().Be("unique-id");
    }

    [Fact]
    public async Task LoadAsync_ProbeDeserializationFailure_SkipsOnlyFailedProbe()
    {
        var configuration = await ProbeConfigurationFileLoader.LoadAsync(
            CreateTempProbeFile(
                """
                [
                  {
                    "id": "invalid-metric",
                    "language": "dotnet",
                    "type": "METRIC_PROBE",
                    "where": { "typeName": "MyClass", "methodName": "MyMethod" },
                    "kind": "INVALID_KIND",
                    "metricName": "my.metric"
                  },
                  {
                    "id": "valid-log",
                    "language": "dotnet",
                    "type": "LOG_PROBE",
                    "where": { "sourceFile": "valid.cs", "lines": ["10"] }
                  }
                ]
                """));

        configuration.Should().NotBeNull();
        configuration!.MetricProbes.Should().BeEmpty();
        configuration.LogProbes.Should().ContainSingle().Which.Id.Should().Be("valid-log");
    }

    [Fact]
    public async Task LoadAsync_LogProbeWithCaptureExpressions_LoadsExpressionsAndLimits()
    {
        var configuration = await ProbeConfigurationFileLoader.LoadAsync(
            CreateTempProbeFile(
                """
                [
                  {
                    "id": "capture-expression-log",
                    "language": "dotnet",
                    "type": "LOG_PROBE",
                    "where": { "typeName": "MyClass", "methodName": "MyMethod" },
                    "captureSnapshot": false,
                    "captureExpressions": [
                      {
                        "name": "inputValue",
                        "expr": {
                          "dsl": "inputValue",
                          "json": { "ref": "inputValue" }
                        },
                        "capture": {
                          "maxReferenceDepth": 1,
                          "maxCollectionSize": 2,
                          "maxLength": 3,
                          "maxFieldCount": 4
                        }
                      }
                    ]
                  }
                ]
                """));

        configuration.Should().NotBeNull();
        var probe = configuration!.LogProbes.Should().ContainSingle().Subject;
        probe.CaptureExpressions.Should().ContainSingle();
        var captureExpression = probe.CaptureExpressions![0];
        captureExpression.Name.Should().Be("inputValue");
        captureExpression.Expr!.Dsl.Should().Be("inputValue");
        captureExpression.Expr.Json!.ToString(Datadog.Trace.Vendors.Newtonsoft.Json.Formatting.None).Should().Be(@"{""ref"":""inputValue""}");
        captureExpression.Capture.Should().Be(new Capture
        {
            MaxReferenceDepth = 1,
            MaxCollectionSize = 2,
            MaxLength = 3,
            MaxFieldCount = 4
        });
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string CreateTempProbeFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        _tempFiles.Add(tempFile);
        File.WriteAllText(tempFile, content);
        return tempFile;
    }
}
