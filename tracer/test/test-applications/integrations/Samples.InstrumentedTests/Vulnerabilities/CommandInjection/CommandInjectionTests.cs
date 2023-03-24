// <copyright file="CommandInjectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;
public class CommandInjectionTests : InstrumentationTestsBase
{ 
    private string taintedProcessName = "nonexisting1.exe";
    private string untaintedProcessName = "nonexisting2.exe";
    private string taintedArgument = "taintedArgument";
    private string untaintedArgument = "untaintedArgument";
    private string vulnerabilityType = "COMMAND_INJECTION";

    public CommandInjectionTests()
    {
        AddTainted(taintedProcessName);
        AddTainted(taintedArgument);
        Environment.SetEnvironmentVariable("PATH", "testPath");
    }

    // Process? Start(ProcessStartInfo startInfo)

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable()
    {
        TestProcessCall(() => Process.Start(new ProcessStartInfo(taintedProcessName) { UseShellExecute = true }));
        AssertVulnerable(vulnerabilityType, taintedProcessName);
    }

    [Fact]
    public void GivenAProcess_WhenStartUntaintedProcess_ThenIsNotVulnerable()
    {
        TestProcessCall(() => Process.Start(new ProcessStartInfo(untaintedProcessName) { UseShellExecute = true }));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable2()
    {
        TestProcessCall(() => Process.Start(new ProcessStartInfo(taintedProcessName) { UseShellExecute = false }));
        AssertVulnerable(vulnerabilityType, taintedProcessName);
    }

    [Fact]
    public void GivenAProcess_WhenStartUntaintedProcess_ThenIsNotVulnerable2()
    {
        TestProcessCall(() => Process.Start(new ProcessStartInfo(untaintedProcessName) { UseShellExecute = false }));
        AssertNotVulnerable();
    }

    // Start(string fileName, string arguments, string userName, SecureString password, string domain)

#pragma warning disable CA1416 // this overload is only supported on Windows
#if NET5_0_OR_GREATER
    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable3()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, untaintedArgument, "user", new SecureString(), "domain"));
        AssertVulnerable(vulnerabilityType, taintedProcessName + " " + untaintedArgument);
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable4()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, taintedArgument, "user", new SecureString(), "domain"));
        AssertVulnerable(vulnerabilityType, untaintedProcessName + " " + taintedArgument);
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsNotVulnerable3()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, untaintedArgument, taintedArgument, new SecureString(), "domain"));
        AssertNotVulnerable();
    }

#endif

    // Process? Start(string fileName, string userName, SecureString password, string domain)

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable5()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, "user", new SecureString(), "domain"));
        AssertVulnerable(vulnerabilityType, taintedProcessName + " " + untaintedArgument);
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsNotVulnerable2()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, "user", new SecureString(), "domain"));
        AssertNotVulnerable();
    }

#pragma warning restore CA1416 // this overload is only supported on Windows

    // Process Start()

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable6()
    {
        Process process = new Process();
        process.StartInfo = new ProcessStartInfo(taintedProcessName, untaintedArgument);
        TestProcessCall(() => process.Start());
        AssertVulnerable(vulnerabilityType, taintedProcessName + " " + untaintedArgument);
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable7()
    {
        Process process = new Process();
        process.StartInfo = new ProcessStartInfo(untaintedProcessName, taintedArgument);
        TestProcessCall(() => process.Start());
        AssertVulnerable(vulnerabilityType, untaintedProcessName + " " + taintedArgument);
    }
    [Fact]
    public void GivenAProcess_WhenStartUntaintedProcess_ThenIsNotVulnerable3()
    {
        Process process = new Process();
        process.StartInfo = new ProcessStartInfo(untaintedProcessName, untaintedArgument);
        TestProcessCall(() => process.Start());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable8()
    {
        Process process = new Process();
        process.StartInfo.FileName = taintedProcessName;
        process.StartInfo.Arguments = untaintedArgument;
        TestProcessCall(() => process.Start());
        AssertVulnerable(vulnerabilityType, taintedProcessName + " " + untaintedArgument);
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable9()
    {
        Process process = new Process();
        process.StartInfo.FileName = untaintedProcessName;
        process.StartInfo.Arguments = taintedArgument;
        TestProcessCall(() => process.Start());
        AssertVulnerable(vulnerabilityType, untaintedProcessName + " " + taintedArgument);
    }

    [Fact]
    public void GivenAProcess_WhenStartUnTaintedProcess_ThenIsNotVulnerable4()
    {
        Process process = new Process();
        process.StartInfo.FileName = untaintedProcessName;
        process.StartInfo.Arguments = untaintedArgument;
        TestProcessCall(() => process.Start());
        AssertNotVulnerable();
    }

    // Process Start(string fileName)

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable10()
    {
        TestProcessCall(() => Process.Start(taintedProcessName));
        AssertVulnerable(vulnerabilityType, taintedProcessName);
    }

    [Fact]
    public void GivenAProcess_WhenStartNotTaintedProcess_ThenIsNotVulnerable5()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName));
        AssertNotVulnerable();
    }

    // Process Start(string fileName, string arguments)

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable11()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, untaintedArgument));
        AssertVulnerable(vulnerabilityType, taintedProcessName + " " + untaintedArgument);
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable12()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, taintedArgument));
        AssertVulnerable(vulnerabilityType, untaintedProcessName + " " + taintedArgument);
    }

    [Fact]
    public void GivenAProcess_WhenStartNotTaintedProcess_ThenIsNotVulnerable6()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, untaintedArgument));
        AssertNotVulnerable();
    }

#if NET5_0_OR_GREATER
    // Process Start(string fileName, string arguments)

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable13()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, new List<string>() { untaintedArgument }));
        AssertVulnerable(vulnerabilityType, taintedProcessName + " " + untaintedArgument);
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable14()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, new List<string>() { taintedArgument }));
        AssertVulnerable(vulnerabilityType, taintedProcessName + " " + taintedArgument);
    }

    [Fact]
    public void GivenAProcess_WhenStartNotTaintedProcess_ThenIsNotVulnerable7()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, new List<string>() { untaintedArgument, untaintedArgument }));
        AssertVulnerable(vulnerabilityType, taintedProcessName + " " + untaintedArgument);
    }
#endif
    private void TestProcessCall(Func<object> expression)
    {
        try
        {
            expression.Invoke();
        }
        catch (Win32Exception) { }
        catch (PlatformNotSupportedException) { }
    }
}
