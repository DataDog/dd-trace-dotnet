// <copyright file="GlobalCoverageMemoryDiagnosticsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0 || NET10_0 || NET11_0

using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Trait("Area", "CIVisibility")]
public class GlobalCoverageMemoryDiagnosticsTests
{
    [Theory]
    [CombinatorialData]
    public void StressArtifactsAreRetainedOnlyOnFailure(bool fail)
    {
        var parentDirectory = Path.Combine(Path.GetTempPath(), "dd-memory-diagnostics-test-" + Path.GetRandomFileName());
        Directory.CreateDirectory(parentDirectory);
        var rootPath = string.Empty;
        Action run = () =>
        {
            using var root = new GlobalCoverageMemoryTests.TemporaryDirectory("dd-global-coverage-diagnostics-", parentDirectory) { DeleteOnDispose = false };
            rootPath = root.RootPath;
            File.WriteAllText(Path.Combine(rootPath, "progress.jsonl"), "memory evidence");
            if (fail)
            {
                throw new InvalidOperationException("Simulated stress failure");
            }

            root.DeleteOnDispose = true;
        };

        try
        {
            if (fail)
            {
                run.Should().Throw<InvalidOperationException>().WithMessage("Simulated stress failure");
            }
            else
            {
                run();
            }

            Path.GetDirectoryName(rootPath).Should().Be(parentDirectory);
            Directory.Exists(rootPath).Should().Be(fail);
            if (fail)
            {
                File.ReadAllText(Path.Combine(rootPath, "progress.jsonl")).Should().Be("memory evidence");
            }
        }
        finally
        {
            Directory.Delete(parentDirectory, recursive: true);
        }
    }
}

#endif
