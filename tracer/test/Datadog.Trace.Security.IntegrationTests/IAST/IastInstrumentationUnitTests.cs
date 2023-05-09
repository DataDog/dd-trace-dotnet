// <copyright file="IastInstrumentationUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

public class IastInstrumentationUnitTests : TestHelper
{
    private List<Type> _taintedTypes = new List<Type>()
    {
        typeof(string), typeof(StringBuilder), typeof(object), typeof(char[]), typeof(object[]), typeof(IEnumerable),
        typeof(string[]), typeof(HashAlgorithm), typeof(SymmetricAlgorithm)
    };

    public IastInstrumentationUnitTests(ITestOutputHelper output)
        : base("InstrumentedTests", output)
    {
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestStringBuilderToStringMethodsAspectCover()
    {
        TestMethodOverloads(typeof(StringBuilder), "ToString", null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestStringBuilderAppendLineMethodsAspectCover()
    {
        TestMethodOverloads(typeof(StringBuilder), "AppendLine", null, true);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestStringBuilderAppendMethodsAspectCover()
    {
        TestMethodOverloads(typeof(StringBuilder), "Append");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestStringBuilderConstructorMethodsAspectCover()
    {
        TestMethodOverloads(typeof(StringBuilder), ".ctor", null, true);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestJoinMethodsAspectCover()
    {
        TestMethodOverloads(typeof(string), "Join");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestToUpperMethodsAspectCover()
    {
        TestMethodOverloads(typeof(string), "ToUpper");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestToUpperInvariantMethodsAspectCover()
    {
        TestMethodOverloads(typeof(string), "ToUpperInvariant");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestToLowerArrayMethodsAspectCover()
    {
        TestMethodOverloads(typeof(string), "ToLower");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestToLowerInvariantMethodsAspectCover()
    {
        TestMethodOverloads(typeof(string), "ToLowerInvariant");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestInsertMethodsAspectCover()
    {
        TestMethodOverloads(typeof(string), "Insert");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestRemoveMethodsAspectCover()
    {
        TestMethodOverloads(typeof(string), "Remove");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestToCharArrayMethodsAspectCover()
    {
        TestMethodOverloads(typeof(string), "ToCharArray");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestTrimStartMethodsAspectCover()
    {
        TestMethodOverloads(typeof(string), "TrimStart");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestTrimEndMethodsAspectCover()
    {
        TestMethodOverloads(typeof(string), "TrimEnd");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestTrimMethodsAspectCover()
    {
        TestMethodOverloads(typeof(string), "Trim");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestSubstringMethodsAspectCover()
    {
        TestMethodOverloads(typeof(string), "Substring");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestConcatMethodsAspectCover()
    {
        var overloadsToExclude = new List<string>() { "System.String Concat(System.Object)" };
        TestMethodOverloads(typeof(string), "Concat", overloadsToExclude);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestAllStringAspectsHaveACorrespondingMethod()
    {
        CheckAllAspectHaveACorrespondingMethod(typeof(string));
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestAllStringBuilderAspectsHaveACorrespondingMethod()
    {
        CheckAllAspectHaveACorrespondingMethod(typeof(StringBuilder));
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestDirectoryClassMethodsAspectCover()
    {
        // load System.Io assembly
        _ = new System.IO.FileInfo("dummy");

        var overloadsToExclude = new List<string>()
        {
            // These methods are not vulnerable or, after evaluation, were considered to report more false positives than actual vulnerabilities
            "Boolean Exists(System.String)",
            "void SetCreationTime(System.String, System.DateTime)",
            "void SetCreationTimeUtc(System.String, System.DateTime)",
            "System.DateTime GetCreationTime(System.String)",
            "System.DateTime GetCreationTimeUtc(System.String)",
            "void SetLastWriteTime(System.String, System.DateTime)",
            "void SetLastWriteTimeUtc(System.String, System.DateTime)",
            "void SetLastAccessTime(System.String, System.DateTime)",
            "void SetLastAccessTimeUtc(System.String, System.DateTime)",
            "System.DateTime GetLastWriteTime(System.String)",
            "System.DateTime GetLastWriteTimeUtc(System.String)",
            "System.DateTime GetLastAccessTime(System.String)",
            "System.DateTime GetLastAccessTimeUtc(System.String)",
            "System.IO.FileSystemInfo CreateSymbolicLink(System.String, System.String)",
            "System.IO.FileSystemInfo ResolveLinkTarget(System.String, Boolean)",
            "System.IO.DirectoryInfo GetParent(System.String)",
#if NETFRAMEWORK
            "System.Security.AccessControl.DirectorySecurity GetAccessControl(System.String)",
            "System.Security.AccessControl.DirectorySecurity GetAccessControl(System.String, System.Security.AccessControl.AccessControlSections)"
#endif
        };
        TestMethodOverloads(typeof(Directory), null, overloadsToExclude, true);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestFileClassMethodsAspectCover()
    {
        var overloadsToExclude = new List<string>()
        {
            "Boolean Exists(System.String)",
            "void SetCreationTime(System.String, System.DateTime)",
            "void SetCreationTimeUtc(System.String, System.DateTime)",
            "System.DateTime GetCreationTime(System.String)",
            "System.DateTime GetCreationTimeUtc(System.String)",
            "void SetLastAccessTime(System.String, System.DateTime)",
            "void SetLastAccessTimeUtc(System.String, System.DateTime)",
            "System.DateTime GetLastAccessTime(System.String)",
            "System.DateTime GetLastAccessTimeUtc(System.String)",
            "void SetLastWriteTime(System.String, System.DateTime)",
            "void SetLastWriteTimeUtc(System.String, System.DateTime)",
            "System.DateTime GetLastWriteTime(System.String)",
            "System.DateTime GetLastWriteTimeUtc(System.String)",
            "System.IO.FileAttributes GetAttributes(System.String)",
            "void Encrypt(System.String)",
            "void Decrypt(System.String)",
            "System.IO.FileSystemInfo CreateSymbolicLink(System.String, System.String)",
            "System.IO.FileSystemInfo ResolveLinkTarget(System.String, Boolean)",
            "System.IO.UnixFileMode GetUnixFileMode(System.String)",
            "void SetUnixFileMode(System.String, System.IO.UnixFileMode)",
#if NETFRAMEWORK
            "System.Security.AccessControl.FileSecurity GetAccessControl(System.String)",
            "System.Security.AccessControl.FileSecurity GetAccessControl(System.String, System.Security.AccessControl.AccessControlSections)",
            "void SetAccessControl(System.String, System.Security.AccessControl.FileSecurity)"
#endif
#if NETCOREAPP3_0
            // special case
            "System.IO.File Move(System.String, System.String, Boolean)"
#endif
        };
        TestMethodOverloads(typeof(File), null, overloadsToExclude, true);

        var aspectsToExclude = new List<string>()
        {
#if NET6_0
            // special case
            "System.IO.File::ReadLinesAsync(System.String, System.Threading.CancellationToken)"
#endif
#if NETCOREAPP2_1
            // special case
            "System.IO.File Move(System.String, System.String, Boolean)"
#endif
        };

        CheckAllAspectHaveACorrespondingMethod(typeof(File), aspectsToExclude);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestDirectoryInfoClassMethodsAspectCover()
    {
        var overloadsToExclude = new List<string>()
        {
            "void CreateAsSymbolicLink(System.String)"
        };
        TestMethodOverloads(typeof(DirectoryInfo), null, overloadsToExclude, true);
        CheckAllAspectHaveACorrespondingMethod(typeof(DirectoryInfo));
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestFileInfoClassMethodsAspectCover()
    {
        var overloadsToExclude = new List<string>()
        {
            "void CreateAsSymbolicLink(System.String)",
#if NETCOREAPP3_0
            // special case
            "void MoveTo(System.String, Boolean)"
#endif
        };
        TestMethodOverloads(typeof(FileInfo), null, overloadsToExclude, true);

        var aspectsToExclude = new List<string>()
        {
#if NETCOREAPP2_1
            // special case
            "void MoveTo(System.String, Boolean)"
#endif
        };

        CheckAllAspectHaveACorrespondingMethod(typeof(FileInfo), aspectsToExclude);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestFileStreamClassMethodsAspectCover()
    {
        var overloadsToExclude = new List<string>() { };
        TestMethodOverloads(typeof(FileStream), ".ctor", overloadsToExclude);
        CheckAllAspectHaveACorrespondingMethod(typeof(FileStream));
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestStreamReaderClassMethodsAspectCover()
    {
        var overloadsToExclude = new List<string>() { };
        TestMethodOverloads(typeof(StreamReader), ".ctor", overloadsToExclude);
        CheckAllAspectHaveACorrespondingMethod(typeof(StreamReader));
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestStreamWriterClassMethodsAspectCover()
    {
        TestMethodOverloads(typeof(StreamWriter), ".ctor");
        CheckAllAspectHaveACorrespondingMethod(typeof(StreamWriter));
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
#else
            if (EnvironmentTools.IsLinux())
            {
                arguments += " --TestCaseFilter:\"Category!=LinuxUnsupported\"";
            }
#endif
            SetEnvironmentVariable("DD_TRACE_LOG_DIRECTORY", Path.Combine(EnvironmentHelper.LogDirectory, "InstrumentedTests"));
            SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "0");
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
            .Replace("[", "<").Replace("]", ">").Replace(",...", string.Empty).Replace("System.", string.Empty);
    }

    private bool MethodShouldBeChecked(MethodBase method)
    {
        var parameters = method.GetParameters();

        if (parameters.Length == 0)
        {
            return true;
        }

        foreach (var parameter in parameters)
        {
            if (_taintedTypes.Contains(parameter.ParameterType))
            {
                return true;
            }
        }

        return false;
    }

    private void TestMethodOverloads(Type typeToCheck, string methodToCheck, List<string> overloadsToExclude = null, bool excludeParameterlessMethods = false)
    {
        var overloadsToExcludeNormalized = overloadsToExclude?.Select(NormalizeName).ToList();
        var aspects = ClrProfiler.AspectDefinitions.Aspects.ToList();
        List<MethodBase> typeMethods = new();
        typeMethods.AddRange(string.IsNullOrEmpty(methodToCheck) ?
            typeToCheck?.GetMethods().Where(x => x.IsPublic && !x.IsVirtual) :
            typeToCheck?.GetMethods().Where(x => x.Name == methodToCheck));

        if (methodToCheck == ".ctor" || string.IsNullOrEmpty(methodToCheck))
        {
            typeMethods.AddRange(typeToCheck.GetConstructors().Where(x => x.IsPublic));
        }

        typeMethods = typeMethods.Where(x => !((x.IsStatic || excludeParameterlessMethods) && x.GetParameters().Count() == 0)).ToList();
        typeMethods.Should().NotBeNull();
        typeMethods.Should().HaveCountGreaterThan(0);

        foreach (var method in typeMethods)
        {
            var methodSignature = NormalizeName(method.ToString());
            if (MethodShouldBeChecked(method) && overloadsToExcludeNormalized?.Contains(methodSignature) != true)
            {
                var isCovered = aspects.Any(x => NormalizeName(x).Contains(methodSignature) && x.Contains(typeToCheck.FullName));
                isCovered.Should().BeTrue(method.ToString() + " is not covered");
            }
        }
    }

    private void CheckAllAspectHaveACorrespondingMethod(Type typeToCheck, List<string> aspectsToExclude = null)
    {
        var aspectsToExcludeNormalized = aspectsToExclude?.Select(NormalizeName).ToList();

        foreach (var aspect in ClrProfiler.AspectDefinitions.Aspects)
        {
            if (aspectsToExcludeNormalized?.FirstOrDefault(x => NormalizeName(x).Contains(x)) is null)
            {
                var index = aspect.IndexOf("::");
                if (index > 0)
                {
                    var index2 = aspect.IndexOf("\"");
                    var aspectType = aspect.Substring(index2 + 1, index - index2 - 1);
                    List<MethodBase> typeMethods = new();
                    typeMethods.AddRange(typeToCheck.GetMethods().Where(x => x.IsPublic).ToList());
                    typeMethods.AddRange(typeToCheck.GetConstructors().Where(x => x.IsPublic).ToList());

                    if (typeToCheck.FullName == aspectType)
                    {
                        var correspondingMethod = typeMethods.FirstOrDefault(x => NormalizeName(aspect).Contains(NormalizeName(x.ToString())));
                        correspondingMethod.Should().NotBeNull(aspect + " is not used");
                    }
                }
            }
        }
    }
}
