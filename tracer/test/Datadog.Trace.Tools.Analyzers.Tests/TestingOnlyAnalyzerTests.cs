// <copyright file="TestingOnlyAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.TestingOnlyAnalyzer.TestingOnlyAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests
{
    public class TestingOnlyAnalyzerTests
    {
        private const string DiagnosticId = TestingOnlyAnalyzer.TestingOnlyAnalyzer.DiagnosticId;

        public static string[] GetTestingOnlyAttributes { get; } = { "TestingOnly", "TestingOnlyAttribute" };

        public static string[] GetTestingAndPrivateOnlyAttributes { get; } = { "TestingAndPrivateOnly", "TestingAndPrivateOnlyAttribute" };

        public static string[] NonTestingOnlyAccesses { get; } =
        {
            "var x = _nonPublicField;",
            "var x = NonPublicProperty;",
            "NonPublicProperty = string.Empty;",
            "var x = PublicSetterProperty;",
            "var x = NonPublicMethod();",
            "var x = NonPublicMethod(x => x, string.Empty);",
            "var x = NonPublicMethod<NonPublicClass>(x => string.Empty, null);",
            "var x = NonPublicMethod(x => string.Empty, (NonPublicClass)null);",
            "var x = new TestClass(string.Empty);",
            "var x = NonPublicClass._publicField;",
            "var x = NonPublicClass.PublicProperty;",
            "var x = NonPublicClass.PublicMethod();",
            "var x = new NonPublicClass();",
        };

        public static string[] TestingOnlyAccesses { get; } =
        {
            "var x = {|#0:_publicField|};",
            "var x = {|#0:PublicProperty|};",
            "{|#0:PublicProperty|} = string.Empty;",
            "{|#0:PublicSetterProperty|} = string.Empty;",
            "var x = {|#0:PublicMethod()|};",
            "var x = {|#0:PublicMethod(x => x, string.Empty)|};",
            "var x = {|#0:NonPublicMethod<PublicClass>(x => string.Empty, null)|};",
            "var x = {|#0:NonPublicMethod(x => string.Empty, (PublicClass)null)|};",
            "var x = {|#0:new TestClass()|};",
            "var x = {|#0:PublicClass._publicField|};",
            "var x = {|#0:PublicClass.PublicProperty|};",
            "var x = {|#0:PublicClass.PublicMethod()|};",
            "var x = {|#0:new PublicClass()|};",
        };

        public static string[] ShouldThrowButDoesnt { get; } =
        {
            """
            IPublic x = new NonPublicClass();
            {|#0:x.PublicMethod()|}; // the explicit interface is marked public so should flag, but doesn't currently
            """,
        };

        public static string[] TestingAndPrivateOnlySameTypeAccesses { get; } =
        {
            // Access members from the same class - should NOT be flagged
            "var x = _publicField;",
            "var x = PublicProperty;",
            "PublicProperty = string.Empty;",
            "var x = PublicMethod();",
            "var x = new TestClass();",
        };

        public static string[] TestingAndPrivateOnlyDifferentTypeAccesses { get; } =
        {
            // Access members from a different class - should be flagged
            "var x = {|#0:OtherClass._testingAndPrivateField|};",
            "var x = {|#0:OtherClass.TestingAndPrivateProperty|};",
            "{|#0:OtherClass.TestingAndPrivateProperty|} = string.Empty;",
            "var x = {|#0:OtherClass.TestingAndPrivateMethod()|};",
            "var x = {|#0:new OtherClass()|};",
        };

        public static IEnumerable<object[]> NonPublicCombination { get; } =
            from attrs in GetTestingOnlyAttributes
            from includeNamespace in new[] { true, false }
            from api in NonTestingOnlyAccesses
            from conditional in new[] { true, false }
            select new object[] { attrs, includeNamespace, api, conditional };

        public static IEnumerable<object[]> PublicCombination { get; } =
            from attrs in GetTestingOnlyAttributes
            from includeNamespace in new[] { true, false }
            from api in TestingOnlyAccesses
            from conditional in new[] { true, false }
            select new object[] { attrs, includeNamespace, api, conditional };

        public static IEnumerable<object[]> NotSupportedCombination { get; } =
            from attrs in GetTestingOnlyAttributes
            from includeNamespace in new[] { true, false }
            from api in ShouldThrowButDoesnt
            from conditional in new[] { true, false }
            select new object[] { attrs, includeNamespace, api, conditional };

        public static IEnumerable<object[]> TestingAndPrivateOnlySameTypeCombination { get; } =
            from attrs in GetTestingAndPrivateOnlyAttributes
            from includeNamespace in new[] { true, false }
            from api in TestingAndPrivateOnlySameTypeAccesses
            from conditional in new[] { true, false }
            select new object[] { attrs, includeNamespace, api, conditional };

        public static IEnumerable<object[]> TestingAndPrivateOnlyDifferentTypeCombination { get; } =
            from attrs in GetTestingAndPrivateOnlyAttributes
            from includeNamespace in new[] { true, false }
            from api in TestingAndPrivateOnlyDifferentTypeAccesses
            from conditional in new[] { true, false }
            select new object[] { attrs, includeNamespace, api, conditional };

        [Fact]
        public async Task EmptySourceShouldNotHaveDiagnostics()
        {
            var test = string.Empty;

            // No diagnostics expected to show up
            await Verifier.VerifyAnalyzerAsync(test);
        }

        [Theory]
        [MemberData(nameof(NonPublicCombination))]
        public async Task ShouldNotFlagUsageOfNonTestingOnly(string publicAttribute, bool includeNamespace, string testFragment, bool attributeIsConditional)
        {
            var code = GetSampleCode(publicAttribute, includeNamespace, testFragment, attributeIsConditional);

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Theory]
        [MemberData(nameof(PublicCombination))]
        public async Task ShouldFlagUsageOfTestingOnly(string publicAttribute, bool includeNamespace, string testFragment, bool attributeIsConditional)
        {
            var code = GetSampleCode(publicAttribute, includeNamespace, testFragment, attributeIsConditional);

            var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
               .WithLocation(0);
            await Verifier.VerifyAnalyzerAsync(code, expected);
        }

        [Theory]
        [MemberData(nameof(NotSupportedCombination))]
        public async Task NotSupported(string publicAttribute, bool includeNamespace, string testFragment, bool attributeIsConditional)
        {
            // We'd like to catch these cases, but they're currently unsupported
            var code = GetSampleCode(publicAttribute, includeNamespace, testFragment, attributeIsConditional);

            var ideallyShouldThrow = async () =>
            {
                var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                   .WithLocation(0);
                await Verifier.VerifyAnalyzerAsync(code, expected);
            };

            // Expect it to throw because there's no warning when there should be.
            await ideallyShouldThrow.Should().ThrowAsync<InvalidOperationException>();
        }

        [Theory]
        [MemberData(nameof(TestingAndPrivateOnlySameTypeCombination))]
        public async Task ShouldNotFlagTestingAndPrivateOnlyWhenCalledFromSameType(string publicAttribute, bool includeNamespace, string testFragment, bool attributeIsConditional)
        {
            var code = GetSampleCode(publicAttribute, includeNamespace, testFragment, attributeIsConditional);

            // No diagnostics expected - calls from within the same type should be allowed
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Theory]
        [MemberData(nameof(TestingAndPrivateOnlyDifferentTypeCombination))]
        public async Task ShouldFlagTestingAndPrivateOnlyWhenCalledFromDifferentType(string publicAttribute, bool includeNamespace, string testFragment, bool attributeIsConditional)
        {
            var code = GetSampleCode(publicAttribute, includeNamespace, testFragment, attributeIsConditional);

            var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
               .WithLocation(0);
            await Verifier.VerifyAnalyzerAsync(code, expected);
        }

        private static string GetSampleCode(string publicAttribute, bool includeNamespace, string testFragment, bool attributeIsConditional)
        {
            var attributePrefix = includeNamespace ? "ConsoleApplication1." : string.Empty;

            var code = $$"""
                using System;
                using System.Collections.Generic;
                using System.Linq;
                using System.Text;
                using System.Threading;
                using System.Threading.Tasks;
                using System.Diagnostics;

                namespace ConsoleApplication1;

                {{GetAttributeDefinition(publicAttribute, attributeIsConditional)}}

                class TestClass
                {
                    private string _nonPublicField;

                    [{{attributePrefix}}{{publicAttribute}}]
                    public string _publicField;

                    internal TestClass(string arg1) { }

                    [{{attributePrefix}}{{publicAttribute}}]
                    public TestClass() { }

                    public string NonPublicProperty { get; set; }

                    [{{attributePrefix}}{{publicAttribute}}]
                    public string PublicProperty { get; set; }

                    public string PublicSetterProperty { get; [{{attributePrefix}}{{publicAttribute}}] set; }

                    public string NonPublicMethod() => "test";

                    [{{attributePrefix}}{{publicAttribute}}]
                    public string PublicMethod() => "test";

                    public string NonPublicMethod<T>(Func<T, string> getter, T arg)
                    {
                        return getter(arg);
                    }

                    [{{attributePrefix}}{{publicAttribute}}]
                    public string PublicMethod<T>(Func<T, string> getter, T arg)
                    {
                        return getter(arg);
                    }

                    public void TestMethod()
                    {
                        {{testFragment}}
                    }
                }

                class NonPublicClass : IPublic
                {
                    public static string _publicField;
                    public NonPublicClass() { }
                    public static string PublicProperty { get; set; }
                    public static string PublicMethod() => "test";

                    [{{attributePrefix}}{{publicAttribute}}]
                    string IPublic.PublicMethod() => "explicit interface";
                }

                [{{attributePrefix}}{{publicAttribute}}]
                class PublicClass
                {
                    public static string _publicField;
                    public PublicClass() { }
                    public static string PublicProperty { get; set; }
                    public static string PublicMethod() => "test";
                }

                // Class for testing different-type access with TestingAndPrivateOnly
                class OtherClass
                {
                    [{{attributePrefix}}{{publicAttribute}}]
                    public static string _testingAndPrivateField;

                    [{{attributePrefix}}{{publicAttribute}}]
                    public OtherClass() { }

                    [{{attributePrefix}}{{publicAttribute}}]
                    public static string TestingAndPrivateProperty { get; set; }

                    [{{attributePrefix}}{{publicAttribute}}]
                    public static string TestingAndPrivateMethod() => "test";
                }

                interface IPublic
                {
                    string PublicMethod();
                }
                """;
            return code;

            static string GetAttributeDefinition(string attribute, bool conditional)
            {
                var attributeName = attribute.EndsWith("Attribute")
                                        ? attribute
                                        : $"{attribute}Attribute";

                var conditionalAttribute = conditional ? "[Conditional(\"DEBUG\")] " : string.Empty;

                return $$"""
            {{conditionalAttribute}}public class {{attributeName}} : Attribute {}
            """;
            }
        }
    }
}
