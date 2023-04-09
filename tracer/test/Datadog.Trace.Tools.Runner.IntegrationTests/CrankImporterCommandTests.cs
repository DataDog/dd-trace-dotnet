// <copyright file="CrankImporterCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.IntegrationTests;

[Collection(nameof(ConsoleTestsCollection))]
public class CrankImporterCommandTests
{
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public void CommandTest()
    {
        var commandLine = "ci crank-import crank-results.json";

        using var console = ConsoleHelper.Redirect();
        var result = Program.Main(commandLine.Split(' '));

        // This should fail because crank-results.json doesn't exist, it doesn't matter because we are testing
        // the execution of the command not the results
        result.Should().Be(1);

        // We check now if the right command ran.
        Assert.Contains("Importing Crank json result file...", console.Output);
        Assert.Contains("FileNotFoundException", console.Output);
        Assert.Contains("crank-results.json", console.Output);
    }
}
