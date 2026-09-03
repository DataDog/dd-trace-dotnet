// <copyright file="StringCaseInterceptionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class StringCaseInterceptionTests
{
    [Fact]
    public void InterceptorTypeExistsOnlyOnNetFramework()
    {
        var type = typeof(Tracer).Assembly.GetType("Datadog.Trace.Generated.Interceptors.StringCaseInterceptors");

#if NETFRAMEWORK
        type.Should().NotBeNull();

        // The generated InterceptsLocationAttribute is `file`-scoped, so the compiler mangles its
        // metadata name (e.g. "<StringCaseInterceptors_g>XXX__InterceptsLocationAttribute") - match
        // on the suffix rather than the exact name.
        type!.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SelectMany(m => m.GetCustomAttributesData())
            .Count(a => a.AttributeType.Name.EndsWith("InterceptsLocationAttribute"))
            .Should()
            .BeGreaterThan(50, "the generator should have intercepted every ToUpperInvariant()/ToLowerInvariant() call site in Datadog.Trace");
#else
        type.Should().BeNull("the interceptor is only ever generated for the .NET Framework build");
#endif
    }
}
