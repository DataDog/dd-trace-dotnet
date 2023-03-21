// <copyright file="IastInstrumentationUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

public class IastInstrumentationUnitTests : TestHelper
{
    private List<Type> _notInstrumentedTypes = new List<Type>() { typeof(ReadOnlyMemory<char>), typeof(ReadOnlySpan<char>), typeof(char*), typeof(bool), typeof(char), typeof(ushort), typeof(ulong), typeof(uint), typeof(int), typeof(byte), typeof(sbyte), typeof(short), typeof(long), typeof(double), typeof(decimal), typeof(float) };

    public IastInstrumentationUnitTests(ITestOutputHelper output)
        : base("InstrumentedTests", output)
    {
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestStringBuilderToStringMethodsAspectCover()
    {
        TestMethodOverloads("System.Text.StringBuilder", "ToString", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestStringBuilderAppendLineMethodsAspectCover()
    {
        var overloadsToExclude = new List<string>() { "System.Text.StringBuilder AppendLine()", "System.Text.StringBuilder AppendLine(AppendInterpolatedStringHandler ByRef)", "System.Text.StringBuilder AppendLine(System.IFormatProvider, AppendInterpolatedStringHandler ByRef)" };
        TestMethodOverloads("System.Text.StringBuilder", "AppendLine", overloadsToExclude, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestStringBuilderAppendMethodsAspectCover()
    {
        var overloadsToExclude = new List<string>() { "System.Text.StringBuilder Append(AppendInterpolatedStringHandler ByRef)", "System.Text.StringBuilder Append(System.IFormatProvider, AppendInterpolatedStringHandler ByRef)" };
        TestMethodOverloads("System.Text.StringBuilder", "Append", overloadsToExclude, _notInstrumentedTypes);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestStringBuilderConstructorMethodsAspectCover()
    {
        TestMethodOverloads("System.Text.StringBuilder", ".ctor", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestJoinMethodsAspectCover()
    {
        TestMethodOverloads("System.String", "Join", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestToUpperMethodsAspectCover()
    {
        TestMethodOverloads("System.String", "ToUpper", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestToUpperInvariantMethodsAspectCover()
    {
        TestMethodOverloads("System.String", "ToUpperInvariant", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestToLowerArrayMethodsAspectCover()
    {
        TestMethodOverloads("System.String", "ToLower", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestToLowerInvariantMethodsAspectCover()
    {
        TestMethodOverloads("System.String", "ToLowerInvariant", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestInsertMethodsAspectCover()
    {
        TestMethodOverloads("System.String", "Insert", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestRemoveMethodsAspectCover()
    {
        TestMethodOverloads("System.String", "Remove", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestToCharArrayMethodsAspectCover()
    {
        TestMethodOverloads("System.String", "ToCharArray", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestTrimStartMethodsAspectCover()
    {
        TestMethodOverloads("System.String", "TrimStart", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestTrimEndMethodsAspectCover()
    {
        TestMethodOverloads("System.String", "TrimEnd", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestTrimMethodsAspectCover()
    {
        TestMethodOverloads("System.String", "Trim", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestSubstringMethodsAspectCover()
    {
        TestMethodOverloads("System.String", "Substring", null, null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestConcatMethodsAspectCover()
    {
        var overloadsToExclude = new List<string>() { "System.String Concat(System.Object)" };
        TestMethodOverloads("System.String", "Concat", overloadsToExclude, _notInstrumentedTypes);
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
        var indexOfTwoColons = signature.IndexOf("::");
        if (indexOfTwoColons > -1)
        {
            signature = signature.Substring(indexOfTwoColons + 2);
        }
        else
        {
            var indexOfFirstSpace = signature.IndexOf(" ");
            if (indexOfFirstSpace > -1)
            {
                signature = signature.Substring(indexOfFirstSpace + 1);
            }
        }

        return signature.Replace(" ", string.Empty).Replace("[T]", string.Empty).Replace("<!!0>", string.Empty)
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
            if (typesToExclude?.Contains(parameter.ParameterType) != true)
            {
                return true;
            }
        }

        return false;
    }

    private void TestMethodOverloads(string typeToCheck, string methodToCheck, List<string> overloadsToExclude, List<Type> typesToExclude)
    {
        var overloadsToExcludeNormalized = overloadsToExclude?.Select(NormalizeName).ToList();
        var aspects = ClrProfiler.AspectDefinitions.Aspects.ToList();
        var type = Type.GetType(typeToCheck);
        type.Should().NotBeNull();
        var typeMethods = type?.GetMethods().Where(x => x.Name == methodToCheck);
        typeMethods.Should().NotBeNull();

        foreach (var method in typeMethods)
        {
            var methodSignature = NormalizeName(method.ToString());
            if (MethodShouldBeChecked(method, typesToExclude) && overloadsToExcludeNormalized?.Contains(methodSignature) != true)
            {
                var isCovered = aspects.Any(x => NormalizeName(x).Contains(methodSignature));
                isCovered.Should().BeTrue(method.ToString() + " is not covered");
            }
        }
    }
}
