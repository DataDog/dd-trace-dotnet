// <copyright file="SealedAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
extern alias AnalyzerCodeFixes;

using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.SealedAnalyzer;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Test = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<
    Datadog.Trace.Tools.Analyzers.SealedAnalyzer.SealedAnalyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.SealedAnalyzer.SealedAnalyzerCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.SealedAnalyzer.SealedAnalyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.SealedAnalyzer.SealedAnalyzerCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.SealedAnalyzer;

public class SealedAnalyzerTests
{
    public static readonly string[] NonPrivateModifiers = [string.Empty, "public ", "internal "];
    public static readonly string[] Modifiers = [..NonPrivateModifiers, "private ", "private protected ", "protected internal "];
    public static readonly string[] ClassTypes = ["class", "record"];

    [Theory]
    [CombinatorialData]
    public async Task SealedClass_NoDiagnostic(
        [CombinatorialMemberData(nameof(Modifiers))] string modifier,
        [CombinatorialMemberData(nameof(ClassTypes))] string type)
    {
        var source = $$"""
                       // Creating outer class so we can test private
                       sealed class OuterClass
                       {
                           {{modifier}}sealed {{type}} TestClass
                           {
                               void TestMethod()
                               {
                               }
                           }
                        }
                       """;

        await Verifier.VerifyAnalyzerAsync(source); // no diagnostics expected
    }

    [Theory]
    [CombinatorialData]
    public async Task NonClass_NoDiagnostic(
        [CombinatorialMemberData(nameof(Modifiers))] string modifier,
        [CombinatorialValues("struct", "enum", "static class", "interface")] string type)
    {
        var source = $$"""
                       // Creating outer class so we can test private
                       sealed class OuterClass
                       {
                           {{modifier}}{{type}} T
                           {
                           }
                       }
                       """;

        await Verifier.VerifyAnalyzerAsync(source); // no diagnostics expected
    }

    [Theory]
    [CombinatorialData]
    public async Task Interface_NoDiagnostic([CombinatorialMemberData(nameof(Modifiers))] string modifier)
    {
        var source = $$"""
                       // Creating outer class so we can test private
                       sealed class OuterClass
                       {
                           {{modifier}}interface TestInterface
                           {
                               void TestMethod()
                               {
                               }
                           }
                       }
                       """;

        await Verifier.VerifyAnalyzerAsync(source); // no diagnostics expected
    }

    [Theory]
    [CombinatorialData]
    public async Task UnsealedClass_Diagnostic(
        [CombinatorialMemberData(nameof(NonPrivateModifiers))] string modifier,
        [CombinatorialMemberData(nameof(ClassTypes))] string type)
    {
        var source =
            $$"""

              {{modifier}}{{type}} {|#0:C|}
              {
                  private int _i;
              }
              """;

        var fixedSource =
            $$"""

              {{modifier}}sealed {{type}} C
              {
                  private int _i;
              }
              """;
        var diagnostic = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithArguments("C").WithLocation(0);
        await VerifyCodeFixAsync(source, diagnostic, fixedSource);
    }

    [Theory]
    [CombinatorialData]
    public async Task UnsealedClassInNamespace_Diagnostic(
        [CombinatorialMemberData(nameof(NonPrivateModifiers))] string modifier,
        [CombinatorialMemberData(nameof(ClassTypes))] string type)
    {
        var source =
            $$"""
              namespace N
              {
                  {{modifier}}{{type}} {|#0:C|}
                  {
                      private int _i;
                  }
              }
              """;

        var fixedSource =
            $$"""
              namespace N
              {
                  {{modifier}}sealed {{type}} C
                  {
                      private int _i;
                  }
              }
              """;
        var diagnostic = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithArguments("C").WithLocation(0);
        await VerifyCodeFixAsync(source, diagnostic, fixedSource);
    }

    [Theory]
    [CombinatorialData]
    public async Task UnsealedNestedClass_Diagnostic(
        [CombinatorialMemberData(nameof(NonPrivateModifiers))] string outerModifier,
        [CombinatorialMemberData(nameof(Modifiers))] string innerModifier,
        [CombinatorialMemberData(nameof(ClassTypes))] string type)
    {
        var source =
            $$"""
              {{outerModifier}}sealed {{type}} Outer
              {
                  {{innerModifier}}{{type}} {|#0:C|}
                  {
                  }
              }
              """;

        var fixedSource =
            $$"""
              {{outerModifier}}sealed {{type}} Outer
              {
                  {{innerModifier}}sealed {{type}} C
                  {
                  }
              }
              """;
        var diagnostic = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithArguments("C").WithLocation(0);
        await VerifyCodeFixAsync(source, diagnostic, fixedSource);
    }

    [Theory]
    [CombinatorialData]
    public async Task UnsealedNestedClassTwoDeep_Diagnostic(
        [CombinatorialMemberData(nameof(NonPrivateModifiers))] string outerModifier,
        [CombinatorialMemberData(nameof(Modifiers))] string middleModifier,
        [CombinatorialMemberData(nameof(Modifiers))] string innerModifier,
        [CombinatorialMemberData(nameof(ClassTypes))] string type)
    {
        var source =
            $$"""
              {{outerModifier}}sealed {{type}} Outer
              {
                  {{middleModifier}}sealed {{type}} Middle
                  {
                      {{innerModifier}}{{type}} {|#0:C|}
                      {
                      }
                  }
              }
              """;

        var fixedSource =
            $$"""
              {{outerModifier}}sealed {{type}} Outer
              {
                  {{middleModifier}}sealed {{type}} Middle
                  {
                      {{innerModifier}}sealed {{type}} C
                      {
                      }
                  }
              }
              """;
        var diagnostic = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithArguments("C").WithLocation(0);
        await VerifyCodeFixAsync(source, diagnostic, fixedSource);
    }

    [Theory]
    [CombinatorialData]
    public async Task DuckTypeAttributedType_NoDiagnostic(
        [CombinatorialMemberData(nameof(NonPrivateModifiers))] string modifier,
        [CombinatorialMemberData(nameof(ClassTypes))] string type)
    {
        var source =
            $$"""
              using System;
              using Datadog.Trace.DuckTyping;
              
              [Datadog.Trace.DuckTyping.DuckType("Sometype", "SomeAssembly")]
              [DuckType("Sometype", "SomeAssembly")]
              {{modifier}}{{type}} C 
              {
              }
              
              namespace Datadog.Trace.DuckTyping
              {
                  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
                  internal sealed class DuckTypeAttribute(string targetType, string targetAssembly) : Attribute
                  {
                      public string? TargetType { get; set; } = targetType;
                  
                      public string? TargetAssembly { get; set; } = targetAssembly;
                  }
              }
              """;

        await Verifier.VerifyAnalyzerAsync(source); // no diagnostics expected
    }

    [Theory]
    [CombinatorialData]
    public async Task ClassWithDerivedType_NoDiagnostic(
        [CombinatorialMemberData(nameof(NonPrivateModifiers))] string modifier,
        [CombinatorialMemberData(nameof(ClassTypes))] string type)
    {
        var source =
            $$"""
              {{modifier}}{{type}} B { }
              {{modifier}}sealed {{type}} D : B { }
              """;

        await Verifier.VerifyAnalyzerAsync(source); // no diagnostics expected
    }

    [Theory]
    [CombinatorialData]
    public async Task AbstractClass_NoDiagnostic([CombinatorialMemberData(nameof(NonPrivateModifiers))] string modifier)
    {
        var source =
            $$"""
              {{modifier}}abstract class B { }
              """;

        await Verifier.VerifyAnalyzerAsync(source); // no diagnostics expected
    }

    [Theory]
    [InlineData("B<T> { }", "D : B<int> { }")]
    [InlineData("B<T> { }", "D<T> : B<T> { }")]
    [InlineData("B<T, U> { }", "D<T> : B<T, int> { }")]
    public async Task GenericClass_WithSubclass_NoDiagnostic_CS(string baseClass, string derivedClass)
    {
        var source = $"""
                      internal class {baseClass}
                      internal sealed class {derivedClass}
                      """;

        await Verifier.VerifyAnalyzerAsync(source); // no diagnostics expected
    }

    [Fact]
    public Task PartialClass_ReportedAndFixedAtAllLocations()
    {
        var test = new Test
        {
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            TestState =
            {
                Sources =
                {
                    @"internal class Base { }",
                    @"internal partial class {|#0:Derived|} : Base { }",
                    @"internal partial class {|#1:Derived|} : Base { }"
                },
                ExpectedDiagnostics =
                {
                    Verifier.Diagnostic(Diagnostics.DiagnosticId).WithArguments("Derived").WithLocation(0).WithLocation(1),
                }
            },
            FixedState =
            {
                Sources =
                {
                    @"internal class Base { }",
                    @"internal sealed partial class Derived : Base { }",
                    @"internal sealed partial class Derived : Base { }"
                }
            }
        };
        return test.RunAsync();
    }

    private static Task VerifyCodeFixAsync(string source, DiagnosticResult expected, string fixedSource)
    {
        // Code fixes are not yet supported for compilation end diagnostics,
        var test = new Test
        {
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            TestCode = source,
            FixedCode = fixedSource,
            ExpectedDiagnostics = { expected },
        };

        return test.RunAsync(CancellationToken.None);
    }
}
