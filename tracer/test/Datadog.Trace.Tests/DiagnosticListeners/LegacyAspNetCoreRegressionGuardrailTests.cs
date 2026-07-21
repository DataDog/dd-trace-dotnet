// <copyright file="LegacyAspNetCoreRegressionGuardrailTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.DiagnosticListeners;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class LegacyAspNetCoreRegressionGuardrailTests
{
    [Fact]
    public void Net461TracerDoesNotReferenceAspNetCoreOrDiagnosticSource()
    {
        var tracerAssembly = typeof(Tracer).Assembly;
        var targetFramework = tracerAssembly.GetCustomAttributes(typeof(TargetFrameworkAttribute), inherit: false)
                                            .Cast<TargetFrameworkAttribute>()
                                            .Single();
        var references = tracerAssembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();

        targetFramework.FrameworkName.Should().Be(".NETFramework,Version=v4.6.1");
        references.Should().NotContain(
            name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal),
            "the net461 tracer must use duck typing instead of compile-time ASP.NET Core references");
        references.Should().NotContain(
            "System.Diagnostics.DiagnosticSource",
            "the net461 tracer loads DiagnosticSource dynamically");
    }
}

#endif
