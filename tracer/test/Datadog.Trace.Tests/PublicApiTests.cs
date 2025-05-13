// <copyright file="PublicApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

extern alias DatadogTraceManual;

using System;
using System.Reflection;
using Datadog.Trace.Annotations;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.PublicApiTests
{
    public class DatadogTraceTests
    {
        private readonly PublicApiTestsBase _base;

        public DatadogTraceTests(ITestOutputHelper output)
        {
            // We don't care about _all_ the public API tests (because this isn't _really_ public)
            // so working around it like this
            _base = new InternalBase(output);
        }

        [Fact]
        public void AssemblyReferencesHaveNotChanged() => _base.AssemblyReferencesHaveNotChanged();

#if NETFRAMEWORK
        [Fact]
        public void ExposesTracingHttpModule()
        {
            var httpModuleType = Type.GetType("Datadog.Trace.AspNet.TracingHttpModule, Datadog.Trace");
            Assert.NotNull(httpModuleType);
        }
#endif

        private class InternalBase(ITestOutputHelper output) : PublicApiTestsBase(typeof(Tracer).Assembly, output);
    }

    public class DatadogTraceAnnotationsTests : PublicApiTestsBase
    {
        public DatadogTraceAnnotationsTests(ITestOutputHelper output)
            : base(typeof(TraceAttribute).Assembly, output)
        {
        }
    }

    public class DatadogTraceManualTests : PublicApiTestsBase
    {
        public DatadogTraceManualTests(ITestOutputHelper output)
            : base(typeof(DatadogTraceManual::Datadog.Trace.Tracer).Assembly, output)
        {
        }
    }
}
