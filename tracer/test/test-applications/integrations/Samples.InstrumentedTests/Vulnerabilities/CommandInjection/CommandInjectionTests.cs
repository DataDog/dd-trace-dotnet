// <copyright file="CommandInjectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;
public class CommandInjectionTests : InstrumentationTestsBase
{ 
    private string taintedProcessName = "nonexisting1.exe";
    private string untaintedProcessName = "nonexisting2.exe";
    private string taintedArgument = "taintedArgument";
    private string untaintedArgument = "untaintedArgument";

    public CommandInjectionTests()
    {
        AddTainted(taintedProcessName);
        AddTainted(taintedArgument);
        Environment.SetEnvironmentVariable("PATH", "testPath");
    }

    // Tests for method Process? Start(ProcessStartInfo startInfo)

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable()
    {
        TestProcessCall(() => Process.Start(new ProcessStartInfo(taintedProcessName) { UseShellExecute = true }));
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+:");
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
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+:");
    }

    [Fact]
    public void GivenAProcess_WhenStartUntaintedProcess_ThenIsNotVulnerable2()
    {
        TestProcessCall(() => Process.Start(new ProcessStartInfo(untaintedProcessName) { UseShellExecute = false }));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAProcess_WhenStartUntaintedProcess_ThenIsNotVulnerable9()
    {
        Assert.Throws<InvalidOperationException>(() => Process.Start(new ProcessStartInfo(null) { UseShellExecute = false }));
    }

    // Tests for method Start(string fileName, string arguments, string userName, SecureString password, string domain)

#pragma warning disable CA1416 // this overload is only supported on Windows
#if NET5_0_OR_GREATER
    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable3()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, untaintedArgument, "user", new SecureString(), "domain"));
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+: untaintedArgument");
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable4()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, taintedArgument, "user", new SecureString(), "domain"));
        AssertVulnerable(commandInjectionType, "nonexisting2.exe :+-taintedArgument-+:");
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable5()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, taintedArgument, "user", new SecureString(), "domain"));
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+: :+-taintedArgument-+:");
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsNotVulnerable3()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, untaintedArgument, taintedArgument, new SecureString(), "domain"));
        AssertNotVulnerable();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable27()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, null, taintedArgument, new SecureString(), "domain"));
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+:");
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable28()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, null, null, new SecureString(), "domain"));
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+:");
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsNotVulnerable8()
    {
        Assert.Throws<ArgumentNullException>(() => Process.Start(taintedProcessName, (List<string>)null));
    }
#endif

    // Process? Start(string fileName, string userName, SecureString password, string domain)

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable6()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, "user", new SecureString(), "domain"));
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+:");
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartUntaintedProcess_ThenIsNotVulnerable4()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, "user", new SecureString(), "domain"));
        AssertNotVulnerable();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStartUntaintedProcess_ThenIsNotVulnerable26()
    {
        Assert.Throws<InvalidOperationException>(() => Process.Start((string) null, "user", new SecureString(), "domain"));
    }

#pragma warning restore CA1416 // this overload is only supported on Windows

    // Tests for method Process Start()

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable7()
    {
        Process process = new Process();
        process.StartInfo = new ProcessStartInfo(taintedProcessName, untaintedArgument);
        TestProcessCall(() => process.Start());
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+: untaintedArgument");
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable8()
    {
        Process process = new Process();
        process.StartInfo = new ProcessStartInfo(untaintedProcessName, taintedArgument);
        TestProcessCall(() => process.Start());
        AssertVulnerable(commandInjectionType, "nonexisting2.exe :+-taintedArgument-+:");
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable9()
    {
        Process process = new Process();
        process.StartInfo = new ProcessStartInfo(taintedProcessName, taintedArgument);
        TestProcessCall(() => process.Start());
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+: :+-taintedArgument-+:");
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
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable10()
    {
        Process process = new Process();
        process.StartInfo.FileName = taintedProcessName;
        process.StartInfo.Arguments = untaintedArgument;
        TestProcessCall(() => process.Start());
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+: untaintedArgument");
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable11()
    {
        Process process = new Process();
        process.StartInfo.FileName = untaintedProcessName;
        process.StartInfo.Arguments = taintedArgument;
        TestProcessCall(() => process.Start());
        AssertVulnerable(commandInjectionType, "nonexisting2.exe :+-taintedArgument-+:");
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable12()
    {
        Process process = new Process();
        process.StartInfo.FileName = taintedProcessName;
        process.StartInfo.Arguments = taintedArgument;
        TestProcessCall(() => process.Start());
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+: :+-taintedArgument-+:");
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

    // Tests for method Process Start(string fileName)

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable13()
    {
        TestProcessCall(() => Process.Start(taintedProcessName));
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+:");
    }

    [Fact]
    public void GivenAProcess_WhenStartNotTaintedProcess_ThenIsNotVulnerable5()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAProcess_WhenStartNotTaintedProcess_ThenIsNotVulnerable25()
    {
        Assert.Throws<InvalidOperationException>(() => Process.Start((string)null));
    }

    // Tests for method Process Start(string fileName, string arguments)

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable14()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, untaintedArgument));
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+: untaintedArgument");
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable15()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, taintedArgument));
        AssertVulnerable(commandInjectionType, "nonexisting2.exe :+-taintedArgument-+:");
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable16()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, taintedArgument));
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+: :+-taintedArgument-+:");
    }

    [Fact]
    public void GivenAProcess_WhenStartNotTaintedProcess_ThenIsNotVulnerable6()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, untaintedArgument));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAProcess_WhenStartNotTaintedProcess_ThenIsNotVulnerable21()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, (string)null));
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+:");
    }

    [Fact]
    public void GivenAProcess_WhenStartNotTaintedProcess_ThenIsNotVulnerable22()
    {
        Assert.Throws<InvalidOperationException>(() => Process.Start((string)null, (string)null));
    }


    [Fact]
    public void GivenAProcess_WhenStartNotTaintedProcess_ThenIsNotVulnerable23()
    {
        Assert.Throws<InvalidOperationException>(() => Process.Start(null, taintedArgument));
    }

#if NET5_0_OR_GREATER
    // Process Start(string fileName, new List<string> arguments)

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable17()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, new List<string>() { untaintedArgument }));
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+: untaintedArgument");
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable18()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, new List<string>() { taintedArgument }));
        AssertVulnerable(commandInjectionType, "nonexisting2.exe :+-taintedArgument-+:");
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable19()
    {
        TestProcessCall(() => Process.Start(taintedProcessName, new List<string>() { taintedArgument, taintedArgument }));
        AssertVulnerable(commandInjectionType, ":+-nonexisting1.exe-+: :+-taintedArgument-+: :+-taintedArgument-+:");
    }

    [Fact]
    public void GivenAProcess_WhenStartNotTaintedProcess_ThenIsNotVulnerable7()
    {
        TestProcessCall(() => Process.Start(untaintedProcessName, new List<string>() { untaintedArgument, untaintedArgument }));
        AssertNotVulnerable();
    }

        [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable20()
    {
        Assert.Throws<ArgumentNullException>(() => Process.Start(taintedProcessName, (List<string>)null));
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
