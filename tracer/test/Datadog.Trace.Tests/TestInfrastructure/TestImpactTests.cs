// <copyright file="TestImpactTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using FluentAssertions;
using TestSelection;
using Xunit;

namespace Datadog.Trace.Tests.TestInfrastructure;

public class TestImpactTests
{
    [Theory]
    [InlineData("README.md", TestArea.All)]
    [InlineData("AGENTS.md", TestArea.All)]
    [InlineData("docs/development/TracerDebugging.md", TestArea.All)]
    [InlineData("docs/development/images/diagram.png", TestArea.All)]
    [InlineData("profiler/docs/MemoryMeasurement.md", TestArea.All)]
    [InlineData("tracer/build/_build/docker/gitlab/UPDATING_IMAGE.md", TestArea.All)]
    [InlineData("tracer/src/Datadog.Trace.Manual/README.md", TestArea.Tracer)]
    [InlineData("tracer/test/Datadog.Trace.Security.Unit.Tests/README.md", TestArea.Tracer | TestArea.Asm)]
    [InlineData("tracer\\src\\Datadog.Trace\\AppSec\\README.md", TestArea.Asm)]
    public void DocumentationUsesNormalImpactRules(string path, TestArea expected)
    {
        TestImpact.GetAffectedAreas(path).Should().Be(expected);
    }

    [Theory]
    [InlineData("new-component/File.cs")]
    [InlineData("tracer/src/Datadog.Trace/NewSubsystem/File.cs")]
    [InlineData("tracer/test/NewTests/Test.cs")]
    [InlineData("tracer/src/Datadog.Trace/Tracer.cs")]
    [InlineData("tracer/src/Datadog.Trace/Configuration/TracerSettings.cs")]
    [InlineData("tracer/src/Datadog.Trace/ClrProfiler/Instrumentation.cs")]
    [InlineData("tracer/src/Datadog.Trace/PDBs/MethodSymbolResolver.cs")]
    [InlineData("shared/src/Datadog.Trace.ClrProfiler.Native/util.h")]
    [InlineData("tracer/test/Datadog.Trace.TestHelpers/Ci/Helper.cs")]
    [InlineData("tracer/test/Datadog.Trace.TestHelpers.AutoInstrumentation/Helper.cs")]
    [InlineData("tracer/test/Datadog.Trace.TestHelpers.SharedSource/Helper.cs")]
    [InlineData("tracer/build/_build/TestImpact.cs")]
    [InlineData(".azure-pipelines/ultimate-pipeline.yml")]
    [InlineData(".github/CODEOWNERS")]
    [InlineData("tracer/src/Datadog.Trace/DebuggerExtensions/File.cs")]
    public void SharedAndUnknownPathsRunAllAreas(string path)
    {
        TestImpact.GetAffectedAreas(path).Should().Be(TestArea.All);
    }

    [Theory]
    [InlineData("tracer/src/Datadog.Trace.Manual/Tracer.cs", TestArea.Tracer)]
    [InlineData("tracer/src/Datadog.Trace/AppSec/Security.cs", TestArea.Asm)]
    [InlineData("tracer/src/Datadog.Trace/Ci/Test.cs", TestArea.CiVisibility)]
    [InlineData("tracer/src/Datadog.Trace/Debugger/LiveDebugger.cs", TestArea.Debugger)]
    [InlineData("profiler/src/ProfilerEngine/Engine.cpp", TestArea.Profiler)]
    [InlineData("tracer/src/Datadog.Trace/ContinuousProfiler/ContextTracker.cs", TestArea.Tracer | TestArea.Profiler)]
    [InlineData("tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AspNetCore/Integration.cs", TestArea.Tracer | TestArea.Asm)]
    [InlineData("tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/XUnit/Integration.cs", TestArea.Tracer | TestArea.Asm | TestArea.CiVisibility)]
    [InlineData("tracer/test/Datadog.Trace.Tests/Ci/Test.cs", TestArea.Tracer | TestArea.CiVisibility)]
    [InlineData("tracer/test/test-applications/integrations/Samples.XUnitTests/Program.cs", TestArea.Tracer | TestArea.CiVisibility)]
    [InlineData("tracer/test/Datadog.Trace.Security.IntegrationTests/Test.cs", TestArea.Tracer | TestArea.Asm)]
    [InlineData("tracer/test/test-applications/debugger/Sample/Program.cs", TestArea.Tracer | TestArea.Debugger)]
    [InlineData("tracer\\src\\Datadog.Trace\\AppSec\\Security.cs", TestArea.Asm)]
    // Matching does not require the file to exist, so deletions retain their area.
    [InlineData("tracer/src/Datadog.Trace/AppSec/DeletedFile.cs", TestArea.Asm)]
    public void KnownPathsSelectTheirDependentAreas(string path, TestArea expected)
    {
        TestImpact.GetAffectedAreas(path).Should().Be(expected);
    }

    [Theory]
    [InlineData("tracer/src/Datadog.Trace/Ci/Directory.Build.props")]
    [InlineData("tracer/src/Datadog.Trace/AppSec/Build.targets")]
    [InlineData("tracer/src/Datadog.Trace.Manual/Datadog.Trace.Manual.csproj")]
    [InlineData("profiler/src/ProfilerEngine/Engine.vcxproj")]
    [InlineData("profiler/src/ProfilerEngine/CMakeLists.txt")]
    [InlineData("profiler/src/ProfilerEngine/Build.cmake")]
    public void BuildInputsCannotBeNarrowedByProductRules(string path)
    {
        TestImpact.GetAffectedAreas(path).Should().Be(TestArea.All);
    }
}
