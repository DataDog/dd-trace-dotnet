// <copyright file="EnumHasFlagAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias AnalyzerCodeFixes;

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.EnumHasFlagAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using AnalyzerVerifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.EnumHasFlagAnalyzer.EnumHasFlagAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.EnumHasFlagAnalyzer.EnumHasFlagAnalyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.EnumHasFlagAnalyzer.EnumHasFlagCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.EnumHasFlagAnalyzer;

public class EnumHasFlagAnalyzerTests
{
    private const string DiagnosticId = Diagnostics.DiagnosticId;

    private const string FlagsEnumDefinition = @"
using System;

[Flags]
enum MyEnum
{
    None = 0,
    A = 1,
    B = 2,
    C = 4,
    All = A | B | C,
}";

    private const string FlagsEnumWithExtension = @"
using System;

[Flags]
enum MyEnum
{
    None = 0,
    A = 1,
    B = 2,
    C = 4,
    All = A | B | C,
}

static class MyEnumExtensions
{
    public static bool HasFlagFast(this MyEnum value, MyEnum flag)
        => flag == 0 ? true : (value & flag) == flag;
}";

    [Fact]
    public async Task EmptySource_NoDiagnostic()
    {
        await AnalyzerVerifier.VerifyAnalyzerAsync(string.Empty);
    }

    [Fact]
    public async Task BitwiseOperation_NoDiagnostic()
    {
        var source = FlagsEnumDefinition + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.A | MyEnum.B;
        var result = (flags & MyEnum.A) != 0;
    }
}";
        await AnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task HasFlagFastCall_NoDiagnostic()
    {
        var source = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.A | MyEnum.B;
        var result = flags.HasFlagFast(MyEnum.A);
    }
}";
        await AnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NonEnumHasFlag_NoDiagnostic()
    {
        var source = @"
class FakeEnum
{
    public bool HasFlag(FakeEnum other) => true;
}

class TestClass
{
    void TestMethod()
    {
        var x = new FakeEnum();
        var result = x.HasFlag(new FakeEnum());
    }
}";
        await AnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task HasFlag_WithoutHasFlagFast_DiagnosticOnly()
    {
        var source = FlagsEnumDefinition + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.A | MyEnum.B;
        var result = {|#0:flags.HasFlag(MyEnum.A)|};
    }
}";
        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning).WithLocation(0);
        await AnalyzerVerifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task HasFlag_WithHasFlagFast_DiagnosticAndFix()
    {
        var source = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.A | MyEnum.B;
        var result = {|#0:flags.HasFlag(MyEnum.A)|};
    }
}";
        var fixedSource = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.A | MyEnum.B;
        var result = flags.HasFlagFast(MyEnum.A);
    }
}";
        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [Fact]
    public async Task HasFlag_CompositeArgument_DiagnosticAndFix()
    {
        var source = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.All;
        var result = {|#0:flags.HasFlag(MyEnum.A | MyEnum.B)|};
    }
}";
        var fixedSource = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.All;
        var result = flags.HasFlagFast(MyEnum.A | MyEnum.B);
    }
}";
        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [Fact]
    public async Task HasFlag_VariableArgument_DiagnosticAndFix()
    {
        var source = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.All;
        var other = MyEnum.A;
        var result = {|#0:flags.HasFlag(other)|};
    }
}";
        var fixedSource = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.All;
        var other = MyEnum.A;
        var result = flags.HasFlagFast(other);
    }
}";
        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [Fact]
    public async Task HasFlag_InIfStatement_DiagnosticAndFix()
    {
        var source = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.A | MyEnum.B;
        if ({|#0:flags.HasFlag(MyEnum.A)|})
        {
        }
    }
}";
        var fixedSource = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.A | MyEnum.B;
        if (flags.HasFlagFast(MyEnum.A))
        {
        }
    }
}";
        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [Fact]
    public async Task HasFlag_Negated_DiagnosticAndFix()
    {
        var source = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.A | MyEnum.B;
        var result = !{|#0:flags.HasFlag(MyEnum.A)|};
    }
}";
        var fixedSource = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.A | MyEnum.B;
        var result = !flags.HasFlagFast(MyEnum.A);
    }
}";
        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [Fact]
    public async Task HasFlag_MultipleCalls_DiagnosticAndFix()
    {
        var source = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.All;
        var a = {|#0:flags.HasFlag(MyEnum.A)|};
        var b = {|#1:flags.HasFlag(MyEnum.B)|};
    }
}";
        var fixedSource = FlagsEnumWithExtension + @"
class TestClass
{
    void TestMethod()
    {
        var flags = MyEnum.All;
        var a = flags.HasFlagFast(MyEnum.A);
        var b = flags.HasFlagFast(MyEnum.B);
    }
}";
        var expected = new[]
        {
            new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning).WithLocation(0),
            new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning).WithLocation(1),
        };
        await Verifier.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [Fact]
    public async Task HasFlag_NonFlagsEnum_DiagnosticOnly()
    {
        var source = @"
enum RegularEnum
{
    One = 1,
    Two = 2,
}

class TestClass
{
    void TestMethod()
    {
        var value = RegularEnum.One;
        var result = {|#0:value.HasFlag(RegularEnum.Two)|};
    }
}";
        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning).WithLocation(0);
        await AnalyzerVerifier.VerifyAnalyzerAsync(source, expected);
    }
}
