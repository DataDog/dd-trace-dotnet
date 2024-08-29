// <copyright file="IastInstrumentationUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.Protocols;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using DirectoryEntry = System.DirectoryServices.DirectoryEntry;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

public class IastInstrumentationUnitTests : TestHelper
{
    private List<Type> _taintedTypes = new List<Type>()
    {
        typeof(string), typeof(StringBuilder), typeof(object), typeof(char[]), typeof(object[]), typeof(IEnumerable),
        typeof(string[]), typeof(HashAlgorithm), typeof(SymmetricAlgorithm), typeof(Uri),
        typeof(System.Net.Http.HttpRequestMessage), typeof(UriBuilder)
    };

    public IastInstrumentationUnitTests(ITestOutputHelper output)
        : base("InstrumentedTests", output)
    {
    }

    [SkippableTheory]
#if NETCOREAPP3_1_OR_GREATER
    [InlineData(typeof(StringBuilder), "Append")]
#else
    [InlineData(typeof(StringBuilder), "Append", new string[] { "System.Text.StringBuilder Append(System.Text.StringBuilder, Int32, Int32)" })]
#endif
    [InlineData(typeof(StringBuilder), "AppendLine", null, true)]
    [InlineData(typeof(StringBuilder), ".ctor", null, true)]
    [InlineData(typeof(StringBuilder), "Insert", null, true)]
#if NETCOREAPP3_1_OR_GREATER
    [InlineData(typeof(StringBuilder), "AppendJoin", new string[] { "System.Text.StringBuilder AppendJoin[T](System.String, System.Collections.Generic.IEnumerable`1[T])" }, true)]
#endif
    [InlineData(typeof(StringBuilder), "Replace", null, true)]
    [InlineData(typeof(StringBuilder), "Remove", null, true)]
    [InlineData(typeof(StringBuilder), "CopyTo", null, true)]
    [InlineData(typeof(StringBuilder), "AppendFormat", new string[] { "System.StringBuilder AppendFormat(System.IFormatProvider,System.Text.CompositeFormat,System.Object[])" }, true)]
#if NETCOREAPP3_1_OR_GREATER
    [InlineData(typeof(string), "Join", new string[] { "System.String Join[T](System.String, System.Collections.Generic.IEnumerable`1[T])" })]
#else
    [InlineData(typeof(string), "Join", new string[] { "System.String Join[T](System.String, System.Collections.Generic.IEnumerable`1[T])", "System.String Join(Char, System.String[])", "System.String Join(Char, System.Object[])", "System.String Join(Char, System.String[], Int32, Int32)" })]
#endif
    [InlineData(typeof(string), "Copy")]
    [InlineData(typeof(string), "ToUpper")]
    [InlineData(typeof(string), "ToUpperInvariant")]
    [InlineData(typeof(string), "ToLower")]
    [InlineData(typeof(string), "ToLowerInvariant")]
    [InlineData(typeof(string), "Insert")]
    [InlineData(typeof(string), "Remove")]
    [InlineData(typeof(string), "ToCharArray")]
    [InlineData(typeof(string), "TrimStart")]
    [InlineData(typeof(string), "Trim")]
    [InlineData(typeof(string), "Substring")]
    [InlineData(typeof(string), "TrimEnd")]
    [InlineData(typeof(string), "Format", new string[] { "System.String Format(System.IFormatProvider, System.Text.CompositeFormat, System.Object[])" })]
#if NETCOREAPP2_1
    [InlineData(typeof(string), "Split", new string[] { "System.String[] Split(System.String, System.StringSplitOptions)", "System.String[] Split(System.String, Int32, System.StringSplitOptions)", "System.String Join(Char, System.String[], Int32, Int32)", "System.String Join(Char, System.String[], Int32, Int32)", "System.String Join(Char, System.String[], Int32, Int32)" })]
#elif NETCOREAPP3_0
    [InlineData(typeof(string), "Split", new string[] { "System.String[] Split(System.String, System.StringSplitOptions)", "System.String[] Split(System.String, Int32, System.StringSplitOptions)" })]
#else
    [InlineData(typeof(string), "Split", new string[] { "System.String[] Split(System.String, System.StringSplitOptions)" })]
#endif
    [InlineData(typeof(string), "Replace", new string[] { "System.String::Replace(System.String,System.String,System.StringComparison)", "System.String::Replace(System.String,System.String,System.Boolean,System.Globalization.CultureInfo)" })]
    [InlineData(typeof(string), "Concat", new string[] { "System.String Concat(System.Object)" })]
    [InlineData(typeof(StreamReader), ".ctor")]
    [InlineData(typeof(StreamWriter), ".ctor")]
    [InlineData(typeof(FileStream), ".ctor")]
    [InlineData(typeof(Random), ".ctor")]
    [InlineData(typeof(DirectoryInfo), null, new string[] { "void CreateAsSymbolicLink(System.String)" }, true)]
    [InlineData(typeof(HttpClient), null, null, true)]
    [InlineData(typeof(HttpMessageInvoker), null, null, true)]
    [InlineData(typeof(WebRequest), null, new string[] { "Boolean RegisterPrefix(System.String, System.Net.IWebRequestCreate)" }, true)]
    [InlineData(typeof(WebClient), null, null, true)]
    [InlineData(typeof(Uri), null, new string[] { "Boolean CheckSchemeName(System.String)", "Boolean IsHexEncoding(System.String, Int32)", "Char HexUnescape(System.String, Int32 ByRef)", "System.UriHostNameType CheckHostName(System.String)", "Boolean op_Equality(System.Uri, System.Uri)", "Boolean op_Inequality(System.Uri, System.Uri)", "Int32 Compare(System.Uri, System.Uri, System.UriComponents, System.UriFormat, System.StringComparison)", "Boolean IsWellFormedUriString(System.String, System.UriKind)", "Boolean IsBaseOf(System.Uri)" }, true)]
    [InlineData(typeof(UriBuilder), null, new string[] { "set_Fragment(System.String)", "get_Fragment(System.String)", "set_Scheme(System.String)", "get_Scheme(System.String)", "set_UserName(System.String)", "get_UserName(System.String)", "set_Password(System.String)", "get_Password(System.String)" }, true)]
    [InlineData(typeof(DirectoryEntry), null, new string[] { "Void Rename(System.String)", "Void RefreshCache(System.String[])", "Void MoveTo(System.DirectoryServices.DirectoryEntry, System.String)", "Void InvokeSet(System.String, System.Object[])", "System.Object InvokeGet(System.String)", "System.Object Invoke(System.String)", "System.Object Invoke(System.String, System.Object[])", "Boolean Exists(System.String)", "System.DirectoryServices.DirectoryEntry CopyTo(System.DirectoryServices.DirectoryEntry, System.String)", "Void set_Username(System.String)", "Void set_Password(System.String)", "Void .ctor(System.Object)" }, true)]
    [InlineData(typeof(DirectorySearcher), null, new string[] { "Void set_AttributeScopeQuery(System.String)" }, true)]
    [InlineData(typeof(PrincipalContext), null, new string[] { "Void .ctor(System.DirectoryServices.AccountManagement.ContextType, System.String)", "Boolean ValidateCredentials(System.String, System.String, System.DirectoryServices.AccountManagement.ContextOptions)", "Boolean ValidateCredentials(System.String, System.String)" }, true)]
    [InlineData(typeof(SearchRequest), null, new string[] { "Void .ctor(System.String, System.Xml.XmlDocument, System.DirectoryServices.Protocols.SearchScope, System.String[])", "void set_RequestId(System.String)", "void set_DistinguishedName(System.String)" }, true)]
    [InlineData(typeof(XmlNode), null, null, true)]
    [InlineData(typeof(Extensions), null, null, true)]
    [InlineData(typeof(XPathExpression), null, null, true)]
    [InlineData(typeof(Activator), "CreateInstance", new string[] { "System.Object CreateInstance(System.Type, System.Reflection.BindingFlags, System.Reflection.Binder, System.Object[], System.Globalization.CultureInfo)", "System.Object CreateInstance(System.Type, System.Object[])", "System.Object CreateInstance(System.Type, System.Object[], System.Object[])", "System.Object CreateInstance(System.Type, System.Reflection.BindingFlags, System.Reflection.Binder, System.Object[], System.Globalization.CultureInfo, System.Object[])", "System.Runtime.Remoting.ObjectHandle CreateInstance(System.ActivationContext, System.String[])" }, true)]
#if NETCOREAPP3_0_OR_GREATER
    [InlineData(typeof(Activator), "CreateInstanceFrom")]
#endif
#if NETFRAMEWORK
    [InlineData(typeof(Activator), "CreateComInstanceFrom")]
#endif
    [InlineData(typeof(Type), "GetType", null, true)]
    [InlineData(typeof(Type), "GetMethod")]
    [InlineData(typeof(Type), "InvokeMember", null, true)]
    [InlineData(typeof(Assembly), "Load", null, true)]
    [InlineData(typeof(Assembly), "LoadFrom", null, true)]
    [InlineData(typeof(SmtpClient), "Send", new[] { "Void Send(System.String, System.String, System.String, System.String)" }, true)]
    [InlineData(typeof(SmtpClient), "SendAsync", new[] { "Void SendAsync(System.String, System.String, System.String, System.String, System.Object)" }, true)]
    [InlineData(typeof(SmtpClient), "SendMailAsync", new[] { "System.Threading.Tasks.Task SendMailAsync(System.String, System.String, System.String, System.String, System.Threading.CancellationToken)", "System.Threading.Tasks.Task SendMailAsync(System.String, System.String, System.String, System.String)" }, true)]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestMethodsAspectCover(Type typeToCheck, string methodToCheck, string[] overloadsToExclude = null, bool excludeParameterlessMethods = false)
    {
        TestMethodOverloads(typeToCheck, methodToCheck, overloadsToExclude?.ToList(), excludeParameterlessMethods);
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

    [Theory]
    [InlineData(typeof(StringBuilder))]
    [InlineData(typeof(string))]
    [InlineData(typeof(StreamWriter))]
    [InlineData(typeof(StreamReader))]
    [InlineData(typeof(FileStream))]
    [InlineData(typeof(DirectoryInfo))]
    [InlineData(typeof(Uri))]
#if NETCOREAPP3_1
    [InlineData(typeof(HttpClient), new string[] { "System.Net.Http.HttpClient::GetStringAsync(System.String,System.Threading.CancellationToken)" })]
    [InlineData(typeof(HttpMessageInvoker), new string[] { "System.Net.Http.HttpMessageInvoker::Send(System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken)", "System.Net.Http.HttpClient::Send(System.Net.Http.HttpRequestMessage)" })]
#else
    [InlineData(typeof(HttpClient))]
    [InlineData(typeof(HttpMessageInvoker))]
#endif
    [InlineData(typeof(WebRequest))]
    [InlineData(typeof(WebClient))]
    [InlineData(typeof(UriBuilder))]
    [InlineData(typeof(DirectoryEntry))]
    [InlineData(typeof(DirectorySearcher))]
    [InlineData(typeof(PrincipalContext))]
    [InlineData(typeof(SearchRequest))]
    [InlineData(typeof(Random))]
    [InlineData(typeof(XmlNode))]
    [InlineData(typeof(Extensions))]
    [InlineData(typeof(XPathExpression))]
    [InlineData(typeof(SmtpClient), new[] { "System.Net.Mail.SmtpClient::SendMailAsync(System.Net.Mail.MailMessage,System.Threading.CancellationToken)\",\"\",[1],[False],[None],Default,[])]" })]
    [InlineData(typeof(Activator), new string[] { "System.Activator::CreateInstance(System.AppDomain,System.String,System.String)" })]
#if !NETFRAMEWORK
#if NET6_0_OR_GREATER
    [InlineData(typeof(Type))]
#else
    [InlineData(typeof(Type), new string[] { "System.Type::GetMethod(System.String,System.Reflection.BindingFlags,System.Type[])" })]
#endif
#else
    [InlineData(typeof(Type), new string[] { "System.Type::GetMethod(System.String,System.Int32,System.Reflection.BindingFlags,System.Reflection.Binder,System.Reflection.CallingConventions,System.Type[],System.Reflection.ParameterModifier[])", "System.Type::GetMethod(System.String,System.Int32,System.Reflection.BindingFlags,System.Reflection.Binder,System.Type[],System.Reflection.ParameterModifier[])", "System.Type::GetMethod(System.String,System.Int32,System.Type[],System.Reflection.ParameterModifier[])", "System.Type::GetMethod(System.String,System.Reflection.BindingFlags,System.Type[])", "System.Type::GetMethod(System.String,System.Int32,System.Type[])" })]
#endif
#if NETFRAMEWORK
    [InlineData(typeof(Assembly))]
#endif
    [InlineData(typeof(Assembly), new string[] { "System.Reflection.Assembly::Load(System.String,System.Security.Policy.Evidence)" })]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestAllAspectsHaveACorrespondingMethod(Type type, string[] aspectsToExclude = null)
    {
        CheckAllAspectHaveACorrespondingMethod(type, aspectsToExclude?.ToList());
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
    public async Task TestInstrumentedUnitTests()
    {
        using (var agent = EnvironmentHelper.GetMockAgent())
        {
            EnableIast(true);
            var logDirectory = Path.Combine(EnvironmentHelper.LogDirectory, "InstrumentedTests");
            SetDumpInfo(logDirectory);
            EnableEvidenceRedaction(false);
            string arguments = string.Empty;
#if NET462
            arguments = @" /Framework:"".NETFramework,Version=v4.6.2"" ";
#else
            if (!EnvironmentTools.IsWindows())
            {
                arguments += (RuntimeInformation.ProcessArchitecture == Architecture.Arm64, EnvironmentHelper.IsAlpine()) switch
                {
                    (true, false) => @" --TestCaseFilter:""(Category!=ArmUnsupported)&(Category!=LinuxUnsupported)""",
                    (true, true) => @" --TestCaseFilter:""(Category!=ArmUnsupported)&(Category!=AlpineArmUnsupported)&(Category!=LinuxUnsupported)""",
                    _ => @" --TestCaseFilter:""Category!=LinuxUnsupported""",
                };
            }
#endif
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "0"); // without this key, ci visibility is enabled for the samples, which we don't really want
            SetEnvironmentVariable("DD_TRACE_LOG_DIRECTORY", logDirectory);
            SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "0");
            ProcessResult processResult = await RunDotnetTestSampleAndWaitForExit(agent, arguments: arguments, forceVsTestParam: true);
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
            .Replace("[", "<").Replace("]", ">").Replace(",...", string.Empty).Replace("System.", string.Empty)
            .Replace("ByRef", string.Empty);
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
        var aspects = ClrProfiler.AspectDefinitions.GetAspects().Where(x => x.Contains(typeToCheck.FullName + "::")).ToList();
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

        foreach (var aspect in ClrProfiler.AspectDefinitions.GetAspects())
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

    private void SetDumpInfo(string logDirectory)
    {
        SetEnvironmentVariable("COMPlus_DbgEnableMiniDump", "1");
        SetEnvironmentVariable("COMPlus_DbgMiniDumpType", "4");
        // Getting: The pid argument is no longer supported when using this one
        // SetEnvironmentVariable("COMPlus_DbgMiniDumpName", logDirectory);
        SetEnvironmentVariable("MINIDUMP_PATH", logDirectory);
    }
}
