// <copyright file="ProcessStartTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Process;
using Xunit;

namespace Datadog.Trace.Instrumented.Iast.Unit.Tests.Vulnerabilities;

public class ProcessStartTests : InstrumentationTestsBase
{
    public ProcessStartTests()
    {
        Environment.SetEnvironmentVariable("PATH", "testPath");
    }

    [Fact]
    public void GivenAProcess_WhenStart_SpanIsGenerated()
    {
        try
        {
            Process.Start(new ProcessStartInfo("nonexisting1.exe") { UseShellExecute = true });
        }
        catch (Win32Exception) { }

        AssertSpanGenerated(ProcessStartCommon.OperationName);
    }

    [Fact]
    public void GivenAProcess_WhenStart_SpanIsGenerated2()
    {
        try
        {
            Process.Start(new ProcessStartInfo("nonexisting2.exe", "arg1") { UseShellExecute = false });
        }
        catch (Win32Exception) { }

        AssertSpanGenerated(ProcessStartCommon.OperationName);
    }

#if NET5_0_OR_GREATER
    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStart_SpanIsGenerated3()
    {
        try
        {
#pragma warning disable CA1416 // this overload is only supported on Windows
            Process.Start("nonexisting3.exe", "arg1", "user", new SecureString(), "domain");
#pragma warning restore CA1416 // this overload is only supported on Windows
        }
        catch (Win32Exception) { }
        catch (PlatformNotSupportedException) { return; }

        AssertSpanGenerated(ProcessStartCommon.OperationName);
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenAProcess_WhenStart_SpanIsGenerated4()
    {
        try
        {
#pragma warning disable CA1416 // this overload is only supported on Windows
            Process.Start("nonexisting4.exe", "user", new SecureString(), "domain");
#pragma warning restore CA1416 // this overload is only supported on Windows
        }
        catch (Win32Exception) { }
        catch (PlatformNotSupportedException) { return;  }

        AssertSpanGenerated(ProcessStartCommon.OperationName);
    }

#endif

    [Fact]
    public void GivenAProcess_WhenStart_SpanIsGenerated5()
    {
        try
        {
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo("nonexisting5.exe", "args");
            process.StartInfo.UseShellExecute = false;
            process.Start();
        }
        catch (Win32Exception) { }

        AssertSpanGenerated(ProcessStartCommon.OperationName);
    }
}
