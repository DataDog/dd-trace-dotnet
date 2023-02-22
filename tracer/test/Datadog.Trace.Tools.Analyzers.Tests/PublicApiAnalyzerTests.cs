// <copyright file="PublicApiAnalyzerTests.cs" company="Datadog">
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
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.PublicApiAnalyzer.PublicApiAnalyzer>;

namespace Datadog.Trace.Tools.Analyzers.Tests
{
    public class PublicApiAnalyzerTests
    {
        private const string DiagnosticId = PublicApiAnalyzer.PublicApiAnalyzer.DiagnosticId;

        public static string[] GetPublicApiAttributes { get; } = { "PublicApi", "PublicApiAttribute" };

        public static string[] NonPublicApiAccesses { get; } =
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

        public static string[] PublicApiAccesses { get; } =
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

        public static IEnumerable<object[]> NonPublicCombination { get; } =
            from attrs in GetPublicApiAttributes
            from includeNamespace in new[] { true, false }
            from api in NonPublicApiAccesses
            from conditional in new[] { true, false }
            select new object[] { attrs, includeNamespace, api, conditional };

        public static IEnumerable<object[]> PublicCombination { get; } =
            from attrs in GetPublicApiAttributes
            from includeNamespace in new[] { true, false }
            from api in PublicApiAccesses
            from conditional in new[] { true, false }
            select new object[] { attrs, includeNamespace, api, conditional };

        public static IEnumerable<object[]> NotSupportedCombination { get; } =
            from attrs in GetPublicApiAttributes
            from includeNamespace in new[] { true, false }
            from api in ShouldThrowButDoesnt
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
        public async Task ShouldNotFlagUsageOfNonPublicApi(string publicAttribute, bool includeNamespace, string testFragment, bool attributeIsConditional)
        {
            var code = GetSampleCode(publicAttribute, includeNamespace, testFragment, attributeIsConditional);

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Theory]
        [MemberData(nameof(PublicCombination))]
        public async Task ShouldFlagUsageOfPublicApi(string publicAttribute, bool includeNamespace, string testFragment, bool attributeIsConditional)
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
            await ideallyShouldThrow.Should().ThrowAsync<EqualWithMessageException>();
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
