// <copyright file="PublicApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

using System;
using System.Reflection;
using Datadog.Trace.Annotations;
using Xunit;

namespace Datadog.Trace.Tests.PublicApiTests
{
    public class DatadogTraceTests : PublicApiTestsBase
    {
        public DatadogTraceTests()
            : base(typeof(Tracer).Assembly)
        {
        }

#if NETFRAMEWORK
        [Fact]
        public void ExposesTracingHttpModule()
        {
            var httpModuleType = Type.GetType("Datadog.Trace.AspNet.TracingHttpModule, Datadog.Trace.AspNet");
            Assert.NotNull(httpModuleType);
        }
#endif
    }

    public class DatadogTraceAnnotationsTests : PublicApiTestsBase
    {
        public DatadogTraceAnnotationsTests()
            : base(typeof(TraceAttribute).Assembly)
        {
        }
    }
}
