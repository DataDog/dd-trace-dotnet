// <copyright file="IastInstrumentationUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

public class IastInstrumentationUnitTests : TestHelper
{
    public IastInstrumentationUnitTests(ITestOutputHelper output)
        : base("InstrumentedTests", output)
    {
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestConcatMethodsAspectCover()
    {
        var overloadsToExclude = new List<string>() { "System.String Concat(System.Object)" };
        var typesToExclude = new List<Type>() { typeof(ReadOnlySpan<char>) };

        TestMethodOverloads("System.String", "Concat", overloadsToExclude, typesToExclude);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestInstrumentedUnitTests()
    {
        using (var agent = EnvironmentHelper.GetMockAgent())
        {
            EnableIast(true);
            string arguments = string.Empty;
#if NET462
            arguments = @" /Framework:"".NETFramework,Version=v4.6.2"" ";
#endif
            SetEnvironmentVariable("DD_TRACE_LOG_DIRECTORY", Path.Combine(EnvironmentHelper.LogDirectory, "InstrumentedTests"));
            ProcessResult processResult = RunDotnetTestSampleAndWaitForExit(agent, arguments: arguments, forceVsTestParam: true);
            processResult.StandardError.Should().BeEmpty("arguments: " + arguments + Environment.NewLine + processResult.StandardError + Environment.NewLine + processResult.StandardOutput);
        }
    }

    private string NormalizeName(string signature)
    {
        return signature.Replace(" ", string.Empty).Replace("::", string.Empty).Replace("[T]", string.Empty).Replace("<!!0>", string.Empty)
            .Replace("[", "<").Replace("]", ">").Replace(",...", string.Empty).Replace("(Char", "(System.Char").Replace(",Int32", ",System.Int32")
            .Replace(",Char", ",System.Char").Replace("(Int32", "(System.Int32");
    }

    private bool MethodShouldBeChecked(MethodInfo method, List<Type> typesToExclude)
    {
        var parameters = method.GetParameters();

        if (parameters.Length == 0)
        {
            return true;
        }

        foreach (var parameter in parameters)
        {
            if (!typesToExclude.Contains(parameter.ParameterType))
            {
                return true;
            }
        }

        return false;
    }

    private void TestMethodOverloads(string typeToCheck, string methodToCheck, List<string> overloadsToExclude, List<Type> typesToExclude)
    {
        var overloadsToExcludeNormalized = overloadsToExclude.Select(NormalizeName).ToList();
        var aspects = Datadog.Trace.ClrProfiler.AspectDefinitions.Aspects.ToList();
        Type type = Type.GetType(typeToCheck);
        var typeMethods = type.GetMethods().Where(x => x.Name == methodToCheck);

        foreach (var method in typeMethods)
        {
            var methodSignature = NormalizeName(method.ToString());
            if (MethodShouldBeChecked(method, typesToExclude) && !overloadsToExcludeNormalized.Contains(methodSignature))
            {
                var isCovered = aspects.Any(x => NormalizeName(x).Contains(methodSignature));
                isCovered.Should().BeTrue(method.ToString() + " is not covered");
            }
        }
    }
}
