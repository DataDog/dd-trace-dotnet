// <copyright file="DebuggerExpressionLanguageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AgileObjects.ReadableExpressions;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    [UsesVerify]
    public class DebuggerExpressionLanguageTests
    {
        private const string DefaultDslTemplate = @"{""dsl"": ""Ignore""}";

        private const string DefaultJsonTemplate = @"{""Ignore"": ""Ignore""}";

        private const string ConditionsFolder = "Conditions";

        private const string TemplatesFolder = "Templates";

        private const string MetricsFolder = "Metrics";

        private static int _staticExpressionInitializedCount;

        public DebuggerExpressionLanguageTests()
        {
            TestObject = new TestStruct
            {
                Collection = ["hello", "1st Item", "2nd item", "3rd item"],
                CollectionInt = [1, 2, 3],
                HashInt = [1, 2, 3],
                Array = ["first", "second"],
                CustomArray = [new TestStruct.NestedObject() { NestedString = "Nested" }, new TestStruct.ChildNestedObject() { NestedString = "Nested Child" }],
                Dictionary = new Dictionary<string, string>
                {
                    { "hello", "world" },
                    { "goodbye", "moon" },
                },
                IntNumber = 42,
                DoubleNumber = 3.14159,
                String = "Hello world!",
                Char = 'C',
                AnotherChar = 'A',
                BooleanValue = true,
                Null = null,
                NullableNullValue = null,
                NullableNotNullValue = new Guid("{00000000-0000-0000-0000-000000000000}"),
                Nested = new TestStruct.NestedObject { NestedString = "Hello from nested object", Nested = new TestStruct.NestedObject { NestedString = "Hello from another nested object" } },
                ChildNested = new TestStruct.ChildNestedObject(),
                ParentAsChildNested = new TestStruct.ChildNestedObject()
            };

            TestObject.Nested.CreateCircleRef();
        }

        internal enum StaticExpressionEnum
        {
            One = 1
        }

        internal TestStruct TestObject { get; set; }

        public static IEnumerable<object[]> SupportedSensitiveDictionaries()
        {
            const string secret = "TOP_SECRET_VALUE";
            yield return [new Dictionary<string, string> { { "password", secret }, { "public", "hello" } }];
            yield return [new SortedDictionary<string, string> { { "password", secret }, { "public", "hello" } }];
            yield return [new ConcurrentDictionary<string, string>(new[] { new KeyValuePair<string, string>("password", secret), new KeyValuePair<string, string>("public", "hello") })];
            yield return [new Hashtable { { "password", secret }, { "public", "hello" } }];
        }

        public static IEnumerable<object[]> SensitiveDictionaryValueOperations()
        {
            yield return
            [
                """
                { "contains": [ "@value", "TOP_SECRET" ] }
                """
            ];
            yield return
            [
                """
                { "startsWith": [ "@value", "TOP" ] }
                """
            ];
            yield return
            [
                """
                { "endsWith": [ "@value", "VALUE" ] }
                """
            ];
            yield return
            [
                """
                { "matches": [ "@value", "TOP_.*" ] }
                """
            ];
            yield return
            [
                """
                { "eq": [ { "substring": [ "@value", 0, 10 ] }, "TOP_SECRET" ] }
                """
            ];
            yield return
            [
                """
                { "gt": [ { "len": "@value" }, 10 ] }
                """
            ];
        }

        public static IEnumerable<object[]> TemplatesResources()
        {
            var sourceFilePath = GetSourceFilePath();
            var path = Path.Combine(sourceFilePath, "..", "ProbeExpressionsResources", TemplatesFolder);
            return Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly).Select(file => new object[] { file });
        }

        public static IEnumerable<object[]> ConditionsResources()
        {
            var sourceFilePath = GetSourceFilePath();
            var path = Path.Combine(sourceFilePath, "..", "ProbeExpressionsResources", ConditionsFolder);
            return Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly).Select(file => new object[] { file });
        }

        public static IEnumerable<object[]> MetricsResources()
        {
            var sourceFilePath = GetSourceFilePath();
            var path = Path.Combine(sourceFilePath, "..", "ProbeExpressionsResources", MetricsFolder);
            return Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly).Select(file => new object[] { file });
        }

        public static string GetSourceFilePath([CallerFilePath] string sourceFilePath = null)
        {
            return sourceFilePath ?? throw new InvalidOperationException("Can't obtain source file path");
        }

        [SkippableTheory]
        [MemberData(nameof(TemplatesResources))]
        public async Task TestTemplates(string expressionTestFilePath)
        {
            await Test(expressionTestFilePath);
        }

        [Theory]
        [MemberData(nameof(ConditionsResources))]
        public async Task TestConditions(string expressionTestFilePath)
        {
            await Test(expressionTestFilePath);
        }

        [Theory]
        [MemberData(nameof(MetricsResources))]
        public async Task TestMetrics(string expressionTestFilePath)
        {
            await Test(expressionTestFilePath);
        }

        [Fact]
        public void ProbeExpressionParser_MethodReflectionCache_UsesStructuralParameterKeys()
        {
            var first = new ProbeExpressionParserHelper.ReflectionMethodIdentifier(typeof(string), nameof(string.Contains), [typeof(string)], null);
            var second = new ProbeExpressionParserHelper.ReflectionMethodIdentifier(typeof(string), nameof(string.Contains), [typeof(string)], null);

            second.Should().Be(first);
            second.GetHashCode().Should().Be(first.GetHashCode());
        }

        [Fact]
        public void ProbeExpressionParser_GenericMethodReflectionCache_UsesStructuralGenericArgumentKeys()
        {
            var first = new ProbeExpressionParserHelper.ReflectionMethodIdentifier(
                typeof(InstanceOfHelper),
                nameof(InstanceOfHelper.IsInstanceOf),
                [typeof(int), typeof(string)],
                [typeof(int)]);
            var second = new ProbeExpressionParserHelper.ReflectionMethodIdentifier(
                typeof(InstanceOfHelper),
                nameof(InstanceOfHelper.IsInstanceOf),
                [typeof(int), typeof(string)],
                [typeof(int)]);

            second.Should().Be(first);
            second.GetHashCode().Should().Be(first.GetHashCode());
        }

        [Fact]
        public void ProbeExpressionParser_GenericMethodReflection_UsesExactNonGenericParameterTypes()
        {
            var method = ProbeExpressionParserHelper.GetMethodByReflection(
                typeof(SameParameterNameOverloads),
                nameof(SameParameterNameOverloads.Match),
                [typeof(string), typeof(string)],
                [typeof(string)]);

            method.Invoke(null, ["value", "generic"]).Should().Be("system");
        }

        [Fact]
        public void ProbeExpressionParser_GenericMethodReflection_MatchesOpenGenericParameterDefinitions()
        {
            var method = ProbeExpressionParserHelper.GetMethodByReflection(
                typeof(Enumerable),
                nameof(Enumerable.Where),
                [typeof(IEnumerable<>), typeof(Func<,>)],
                [typeof(string)]);

            method.GetParameters()[0].ParameterType.GetGenericTypeDefinition().Should().Be(typeof(IEnumerable<>));
        }

        [Fact]
        public void ProbeExpressionParser_ObjectReturnType_AllowsValueTypeResults()
        {
            // Arrange
            var scopeMembers = CreateScopeMembers();
            const string json = """
                                {
                                  "ref": "@duration"
                                }
                                """;

            // Act
            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<TimeSpan>(result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_NonNullableValueTypeComparedToNull_DoesNotThrow()
        {
            var scopeMembers = CreateScopeMembers();

            const string equalsJson = """
                                      {
                                        "eq": [
                                          { "ref": "IntLocal" },
                                          null
                                        ]
                                      }
                                      """;

            const string notEqualsJson = """
                                         {
                                           "ne": [
                                             { "ref": "IntLocal" },
                                             null
                                           ]
                                         }
                                         """;

            var equalsCompiled = ProbeExpressionParser<bool>.ParseExpression(equalsJson, scopeMembers);
            var equalsResult = equalsCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.False(equalsResult);
            Assert.True(equalsCompiled.Errors == null || equalsCompiled.Errors.Length == 0);

            var notEqualsCompiled = ProbeExpressionParser<bool>.ParseExpression(notEqualsJson, scopeMembers);
            var notEqualsResult = notEqualsCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(notEqualsResult);
            Assert.True(notEqualsCompiled.Errors == null || notEqualsCompiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_ValueTypeNull_UsesDefaultValue()
        {
            // Arrange
            var scopeMembers = CreateScopeMembers();
            scopeMembers.Duration = new ScopeMember("@duration", typeof(TimeSpan), null, ScopeMemberKind.Duration);

            const string json = """
                                {
                                  "ref": "@duration"
                                }
                                """;

            // Act
            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<TimeSpan>(result);
            Assert.Equal(default(TimeSpan), (TimeSpan)result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_OpenGenericReferenceTypeParameter_CanCompileExpression()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.InvocationTarget = new ScopeMember("this", typeof(GenericReferenceTypeTarget<>), new GenericReferenceTypeTarget<string>(), ScopeMemberKind.This);

            const string json = """
                                {
                                  "ref": "this"
                                }
                                """;

            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers, typeof(GenericReferenceTypeTarget<>));
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.IsType<GenericReferenceTypeTarget<string>>(result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionEvaluator_CaptureExpressions_EvaluatesObjectValues()
        {
            var scopeMembers = CreateScopeMembers();
            var evaluator = new ProbeExpressionEvaluator(
                templates: null,
                condition: null,
                metric: null,
                spanDecorations: null,
                captureExpressions:
                [
                    new CaptureExpressionDefinition("inputValue", new DebuggerExpression(string.Empty, @"{""ref"":""StringArg""}", null), default),
                    new CaptureExpressionDefinition("collection_first_element", new DebuggerExpression(string.Empty, @"{""index"":[{""ref"":""CollectionLocal""},0]}", null), default),
                    new CaptureExpressionDefinition("dictionary_value", new DebuggerExpression(string.Empty, @"{""index"":[{""ref"":""DictionaryLocal""},""goodbye""]}", null), default),
                    new CaptureExpressionDefinition("nested_field", new DebuggerExpression(string.Empty, @"{""getmember"":[{""ref"":""NestedObjectLocal""},""NestedString""]}", null), default)
                ]);

            ExpressionEvaluationResult result = default;
            evaluator.EvaluateCaptureExpressions(ref result, scopeMembers);

            result.CaptureExpressionCount.Should().Be(4);
            result.CaptureExpressions.Should().HaveCountGreaterThanOrEqualTo(result.CaptureExpressionCount);
            result.CaptureExpressions[0].Name.Should().Be("inputValue");
            result.CaptureExpressions[0].Value.Should().Be("Hello world!");
            result.CaptureExpressions[0].Type.Should().Be(typeof(string));
            result.CaptureExpressions[1].Value.Should().Be("hello");
            result.CaptureExpressions[2].Value.Should().Be("moon");
            result.CaptureExpressions[3].Value.Should().Be("Hello from nested object");
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ProbeExpressionEvaluator_CaptureExpressionRootFilter_StopsAfterCaptureLimitAndOneExtraMatch()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember("FilteredCollectionLocal", typeof(List<string>), new List<string> { "hello", "world", "again" }, ScopeMemberKind.Local));
            var captureLimitInfo = new CaptureLimitInfo(
                MaxReferenceDepth: 5,
                MaxCollectionSize: 1,
                MaxLength: 255,
                MaxFieldCount: 20);
            var evaluator = new ProbeExpressionEvaluator(
                templates: null,
                condition: null,
                metric: null,
                spanDecorations: null,
                captureExpressions:
                [
                    new CaptureExpressionDefinition(
                        "filtered",
                        new DebuggerExpression(string.Empty, @"{""filter"":[{""ref"":""FilteredCollectionLocal""},{""gt"":[{""len"":""@it""},0]}]}", null),
                        captureLimitInfo)
                ]);

            ExpressionEvaluationResult result = default;
            evaluator.EvaluateCaptureExpressions(ref result, scopeMembers);

            result.CaptureExpressionCount.Should().Be(1);
            result.CaptureExpressions[0].Value.Should().BeAssignableTo<IBoundedCaptureCollectionResult>();
            var filtered = (IBoundedCaptureCollectionResult)result.CaptureExpressions[0].Value;
            filtered.Count.Should().Be(1);
            filtered.WasTruncated.Should().BeTrue();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ProbeExpressionEvaluator_CaptureExpressionRootFilter_UnderCaptureLimitDoesNotTruncate()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember("FilteredCollectionLocal", typeof(List<string>), new List<string> { "hello", "moon", "cat" }, ScopeMemberKind.Local));
            var evaluator = new ProbeExpressionEvaluator(
                templates: null,
                condition: null,
                metric: null,
                spanDecorations: null,
                captureExpressions:
                [
                    new CaptureExpressionDefinition(
                        "filtered",
                        new DebuggerExpression(string.Empty, @"{""filter"":[{""ref"":""FilteredCollectionLocal""},{""gt"":[{""len"":""@it""},4]}]}", null),
                        new CaptureLimitInfo(MaxReferenceDepth: 5, MaxCollectionSize: 2, MaxLength: 255, MaxFieldCount: 20))
                ]);

            ExpressionEvaluationResult result = default;
            evaluator.EvaluateCaptureExpressions(ref result, scopeMembers);

            result.CaptureExpressionCount.Should().Be(1);
            result.CaptureExpressions[0].Value.Should().BeAssignableTo<IBoundedCaptureCollectionResult>();
            var filtered = (IBoundedCaptureCollectionResult)result.CaptureExpressions[0].Value;
            filtered.Count.Should().Be(1);
            filtered.WasTruncated.Should().BeFalse();
            ((IEnumerable<string>)result.CaptureExpressions[0].Value).Should().ContainSingle().Which.Should().Be("hello");
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ProbeExpressionEvaluator_CaptureExpressionRootFilter_CanReferenceMethodScopeMembersInPredicate()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember("FilteredCollectionLocal", typeof(List<int>), new List<int> { 1, 2, 3, 4 }, ScopeMemberKind.Local));
            scopeMembers.AddMember(new ScopeMember("FilterThresholdLocal", typeof(int), 2, ScopeMemberKind.Local));
            var evaluator = new ProbeExpressionEvaluator(
                templates: null,
                condition: null,
                metric: null,
                spanDecorations: null,
                captureExpressions:
                [
                    new CaptureExpressionDefinition(
                        "filtered",
                        new DebuggerExpression(string.Empty, @"{""filter"":[{""ref"":""FilteredCollectionLocal""},{""gt"":[""@it"",{""ref"":""FilterThresholdLocal""}]}]}", null),
                        new CaptureLimitInfo(MaxReferenceDepth: 5, MaxCollectionSize: 2, MaxLength: 255, MaxFieldCount: 20))
                ]);

            ExpressionEvaluationResult result = default;
            evaluator.EvaluateCaptureExpressions(ref result, scopeMembers);

            result.CaptureExpressionCount.Should().Be(1);
            result.CaptureExpressions[0].Value.Should().BeAssignableTo<IBoundedCaptureCollectionResult>();
            var filtered = (IBoundedCaptureCollectionResult)result.CaptureExpressions[0].Value;
            filtered.Count.Should().Be(2);
            filtered.WasTruncated.Should().BeFalse();
            ((IEnumerable<int>)result.CaptureExpressions[0].Value).Should().Equal(3, 4);
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ProbeExpressionParser_AnyPredicate_CanReferenceMethodScopeMembers()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember("FilterThresholdLocal", typeof(int), 2, ScopeMemberKind.Local));
            const string json = """
                                {
                                  "any": [
                                    { "ref": "CollectionIntLocal" },
                                    { "gt": [ "@it", { "ref": "FilterThresholdLocal" } ] }
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<bool>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Theory]
        [InlineData("any")]
        [InlineData("all")]
        public void ProbeExpressionParser_PredicateWithUndefinedSource_DoesNotAddSecondaryCollectionError(string operation)
        {
            var scopeMembers = CreateScopeMembers();
            var json = $$"""
                         {
                           "{{operation}}": [
                             { "ref": "MissingCollectionLocal" },
                             true
                           ]
                         }
                         """;

            var compiled = ProbeExpressionParser<bool>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(result);
            compiled.Errors.Should().ContainSingle();
            compiled.Errors[0].Message.Should().Contain("The property or field does not exist");
        }

        [Fact]
        public void ProbeExpressionParser_FilterWithUndefinedSource_DoesNotAddSecondaryCollectionError()
        {
            var scopeMembers = CreateScopeMembers();
            const string json = """
                                {
                                  "filter": [
                                    { "ref": "MissingCollectionLocal" },
                                    true
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Same(UndefinedValue.Instance, result);
            compiled.Errors.Should().ContainSingle();
            compiled.Errors[0].Message.Should().Contain("The property or field does not exist");
        }

        [Fact]
        public void ProbeExpressionParser_NestedFilterWithUndefinedSource_DoesNotAddSecondaryCollectionError()
        {
            var scopeMembers = CreateScopeMembers();
            const string json = """
                                {
                                  "filter": [
                                    {
                                      "filter": [
                                        { "ref": "MissingCollectionLocal" },
                                        true
                                      ]
                                    },
                                    true
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<object>.ParseCaptureExpression(
                json,
                scopeMembers,
                new CaptureLimitInfo(MaxReferenceDepth: 5, MaxCollectionSize: 2, MaxLength: 255, MaxFieldCount: 20));
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Same(UndefinedValue.Instance, result);
            compiled.Errors.Should().ContainSingle();
            compiled.Errors[0].Message.Should().Contain("The property or field does not exist");
        }

        [Fact]
        public void ProbeExpressionEvaluator_CaptureExpressionRootDictionaryFilter_KeepsDictionaryMetadata()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember(
                "FilteredDictionaryLocal",
                typeof(Dictionary<string, string>),
                new Dictionary<string, string>
                {
                    { "one", "first" },
                    { "two", "second" },
                    { "three", "third" },
                },
                ScopeMemberKind.Local));
            var evaluator = new ProbeExpressionEvaluator(
                templates: null,
                condition: null,
                metric: null,
                spanDecorations: null,
                captureExpressions:
                [
                    new CaptureExpressionDefinition(
                        "filtered",
                        new DebuggerExpression(string.Empty, @"{""filter"":[{""ref"":""FilteredDictionaryLocal""},{""contains"":[""@value"",""i""]}]}", null),
                        new CaptureLimitInfo(MaxReferenceDepth: 5, MaxCollectionSize: 1, MaxLength: 255, MaxFieldCount: 20))
                ]);

            ExpressionEvaluationResult result = default;
            evaluator.EvaluateCaptureExpressions(ref result, scopeMembers);

            result.CaptureExpressionCount.Should().Be(1);
            result.CaptureExpressions[0].Value.Should().BeAssignableTo<IBoundedCaptureCollectionResult>();
            var filtered = (IBoundedCaptureCollectionResult)result.CaptureExpressions[0].Value;
            filtered.Count.Should().Be(1);
            filtered.WasTruncated.Should().BeTrue();
            filtered.IsDictionary.Should().BeTrue();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ProbeExpressionEvaluator_CaptureExpressionRootFilterChain_StopsAfterCaptureLimitAndOneExtraMatch()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember("FilterChainCollectionLocal", typeof(List<string>), new List<string> { "a", "bb", "ccc", "dddd", "ddddd" }, ScopeMemberKind.Local));
            var evaluator = new ProbeExpressionEvaluator(
                templates: null,
                condition: null,
                metric: null,
                spanDecorations: null,
                captureExpressions:
                [
                    new CaptureExpressionDefinition(
                        "filtered",
                        new DebuggerExpression(
                            string.Empty,
                            @"{""filter"":[{""filter"":[{""ref"":""FilterChainCollectionLocal""},{""gt"":[{""len"":""@it""},1]}]},{""startsWith"":[""@it"",""d""]}]}",
                            null),
                        new CaptureLimitInfo(MaxReferenceDepth: 5, MaxCollectionSize: 1, MaxLength: 255, MaxFieldCount: 20))
                ]);

            ExpressionEvaluationResult result = default;
            evaluator.EvaluateCaptureExpressions(ref result, scopeMembers);

            result.CaptureExpressionCount.Should().Be(1);
            result.CaptureExpressions[0].Value.Should().BeAssignableTo<IBoundedCaptureCollectionResult>();
            var filtered = (IBoundedCaptureCollectionResult)result.CaptureExpressions[0].Value;
            filtered.Count.Should().Be(1);
            filtered.WasTruncated.Should().BeTrue();
            ((IEnumerable<string>)result.CaptureExpressions[0].Value).Should().ContainSingle().Which.Should().Be("dddd");
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ProbeExpressionEvaluator_CaptureExpressionRootFilter_MaterializesNestedFilterUnderIndex()
        {
            var scopeMembers = CreateScopeMembers();
            var nestedCollections = new List<List<string>>
            {
                new() { "a" },
                new() { "hello", "world", "again" },
            };
            scopeMembers.AddMember(new ScopeMember("NestedCollectionsLocal", typeof(List<List<string>>), nestedCollections, ScopeMemberKind.Local));
            var evaluator = new ProbeExpressionEvaluator(
                templates: null,
                condition: null,
                metric: null,
                spanDecorations: null,
                captureExpressions:
                [
                    new CaptureExpressionDefinition(
                        "filtered",
                        new DebuggerExpression(
                            string.Empty,
                            @"{""filter"":[{""index"":[{""filter"":[{""ref"":""NestedCollectionsLocal""},{""gt"":[{""len"":""@it""},1]}]},0]},{""gt"":[{""len"":""@it""},0]}]}",
                            null),
                        new CaptureLimitInfo(MaxReferenceDepth: 5, MaxCollectionSize: 1, MaxLength: 255, MaxFieldCount: 20))
                ]);

            ExpressionEvaluationResult result = default;
            evaluator.EvaluateCaptureExpressions(ref result, scopeMembers);

            result.CaptureExpressionCount.Should().Be(1);
            result.CaptureExpressions[0].Value.Should().BeAssignableTo<IBoundedCaptureCollectionResult>();
            var filtered = (IBoundedCaptureCollectionResult)result.CaptureExpressions[0].Value;
            filtered.Count.Should().Be(1);
            filtered.WasTruncated.Should().BeTrue();
            ((IEnumerable<string>)result.CaptureExpressions[0].Value).Should().ContainSingle().Which.Should().Be("hello");
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ProbeExpressionEvaluator_CaptureExpressionRootFilter_MaterializesNestedDictionaryFilterUnderIndex()
        {
            var scopeMembers = CreateScopeMembers();
            var dictionary = new Dictionary<string, List<string>>
            {
                { "first", new List<string> { "skip" } },
                { "target", new List<string> { "alpha", "beta", "gamma" } },
            };
            scopeMembers.AddMember(new ScopeMember("NestedDictionaryLocal", typeof(Dictionary<string, List<string>>), dictionary, ScopeMemberKind.Local));
            var evaluator = new ProbeExpressionEvaluator(
                templates: null,
                condition: null,
                metric: null,
                spanDecorations: null,
                captureExpressions:
                [
                    new CaptureExpressionDefinition(
                        "filtered",
                        new DebuggerExpression(
                            string.Empty,
                            @"{""filter"":[{""getmember"":[{""index"":[{""filter"":[{""ref"":""NestedDictionaryLocal""},{""eq"":[""@key"",""target""]}]},0]},""Value""]},{""gt"":[{""len"":""@it""},0]}]}",
                            null),
                        new CaptureLimitInfo(MaxReferenceDepth: 5, MaxCollectionSize: 1, MaxLength: 255, MaxFieldCount: 20))
                ]);

            ExpressionEvaluationResult result = default;
            evaluator.EvaluateCaptureExpressions(ref result, scopeMembers);

            result.CaptureExpressionCount.Should().Be(1);
            result.CaptureExpressions[0].Value.Should().BeAssignableTo<IBoundedCaptureCollectionResult>();
            var filtered = (IBoundedCaptureCollectionResult)result.CaptureExpressions[0].Value;
            filtered.Count.Should().Be(1);
            filtered.WasTruncated.Should().BeTrue();
            ((IEnumerable<string>)result.CaptureExpressions[0].Value).Should().ContainSingle().Which.Should().Be("alpha");
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void FilterEvaluationHelpers_FilterForCapture_StopsAfterLimitAndOneExtraMatch()
        {
            var collection = new CountingEnumerable<string>(["hello", "world", "again"]);

            var result = FilterEvaluationHelpers.FilterForCapture(collection, static value => value.Length > 0, maxCollectionSize: 1, isDictionary: false);

            result.Count.Should().Be(1);
            result.WasTruncated.Should().BeTrue();
            collection.VisitedItems.Should().Be(2);
        }

        [Fact]
        public void ProbeExpressionEvaluator_LenOfFilter_KeepsExactSemantics()
        {
            var scopeMembers = CreateScopeMembers();
            var evaluator = new ProbeExpressionEvaluator(
                templates: null,
                condition: null,
                metric: null,
                spanDecorations: null,
                captureExpressions:
                [
                    new CaptureExpressionDefinition(
                        "filteredLength",
                        new DebuggerExpression(string.Empty, @"{""len"":{""filter"":[{""ref"":""CollectionLocal""},{""gt"":[{""len"":""@it""},4]}]}}", null),
                        new CaptureLimitInfo(
                            MaxReferenceDepth: 5,
                            MaxCollectionSize: 1,
                            MaxLength: 255,
                            MaxFieldCount: 20))
                ]);

            ExpressionEvaluationResult result = default;
            evaluator.EvaluateCaptureExpressions(ref result, scopeMembers);

            result.CaptureExpressionCount.Should().Be(1);
            result.CaptureExpressions[0].Value.Should().Be(4);
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ProbeExpressionEvaluator_CaptureExpressions_DoesNotCaptureParseFailures()
        {
            var scopeMembers = CreateScopeMembers();
            var evaluator = new ProbeExpressionEvaluator(
                templates: null,
                condition: null,
                metric: null,
                spanDecorations: null,
                captureExpressions:
                [
                    new CaptureExpressionDefinition("invalid", new DebuggerExpression(string.Empty, @"{""gt"":[{""ref"":""StringArg""},2]}", null), default)
                ]);

            ExpressionEvaluationResult result = default;
            evaluator.EvaluateCaptureExpressions(ref result, scopeMembers);

            result.CaptureExpressionCount.Should().Be(0);
            result.CaptureExpressions.Should().BeNull();
            result.Errors.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_ResolvedCustomerType_EvaluatesCondition()
        {
            try
            {
                InstanceOfHelper.ResetForTests();
                var scopeMembers = CreateScopeMembers();
                var typeName = typeof(TestStruct.NestedObject).FullName;
                var evaluator = new ProbeExpressionEvaluator(
                    templates: null,
                    condition: new DebuggerExpression(string.Empty, CreateInstanceOfJson(@"{""ref"":""NestedObjectLocal""}", typeName), null),
                    metric: null,
                    spanDecorations: null,
                    captureExpressions: null);

                var result = evaluator.Evaluate(scopeMembers);

                result.Condition.Should().BeTrue();
                result.Errors.Should().BeNullOrEmpty();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_CustomerTypeName_RequiresExactCasing()
        {
            try
            {
                InstanceOfHelper.ResetForTests();
                var scopeMembers = CreateScopeMembers();
                var typeName = typeof(TestStruct.NestedObject).FullName.ToUpperInvariant();
                var evaluator = new ProbeExpressionEvaluator(
                    templates: null,
                    condition: new DebuggerExpression(string.Empty, CreateInstanceOfJson(@"{""ref"":""NestedObjectLocal""}", typeName), null),
                    metric: null,
                    spanDecorations: null,
                    captureExpressions: null);

                var result = evaluator.Evaluate(scopeMembers);

                result.Condition.Should().BeTrue();
                result.HasConditionError.Should().BeTrue();
                result.Errors.Should().ContainSingle();
                result.Errors[0].Message.Should().Contain("unknown type");
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_ValueType_EvaluatesCondition()
        {
            try
            {
                InstanceOfHelper.ResetForTests();
                var scopeMembers = CreateScopeMembers();
                var evaluator = new ProbeExpressionEvaluator(
                    templates: null,
                    condition: new DebuggerExpression(string.Empty, CreateInstanceOfJson(@"{""ref"":""IntLocal""}", typeof(int).FullName), null),
                    metric: null,
                    spanDecorations: null,
                    captureExpressions: null);

                var result = evaluator.Evaluate(scopeMembers);

                result.Condition.Should().BeTrue();
                result.Errors.Should().BeNullOrEmpty();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Theory]
        [InlineData("string")]
        [InlineData("int")]
        [InlineData("double")]
        public void ProbeExpressionParser_InstanceOf_BclAlias_EvaluatesCondition(string typeName)
        {
            try
            {
                InstanceOfHelper.ResetForTests();
                var scopeMembers = CreateScopeMembers();
                var source = typeName.Equals("string", StringComparison.OrdinalIgnoreCase)
                                 ? @"{""ref"":""StringLocal""}"
                                 : typeName.Equals("int", StringComparison.OrdinalIgnoreCase)
                                     ? @"{""ref"":""IntLocal""}"
                                     : @"{""ref"":""DoubleLocal""}";
                var evaluator = new ProbeExpressionEvaluator(
                    templates: null,
                    condition: new DebuggerExpression(string.Empty, CreateInstanceOfJson(source, typeName), null),
                    metric: null,
                    spanDecorations: null,
                    captureExpressions: null);

                var result = evaluator.Evaluate(scopeMembers);

                result.Condition.Should().BeTrue();
                result.Errors.Should().BeNullOrEmpty();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Theory]
        [InlineData("string")]
        [InlineData("System.String")]
        [InlineData("int")]
        [InlineData("System.Int32")]
        [InlineData("Guid")]
        [InlineData("System.Guid")]
        [InlineData("DateTime")]
        [InlineData("System.DateTimeOffset")]
        [InlineData("TimeSpan")]
        [InlineData("Type")]
        [InlineData("Exception")]
        [InlineData("Enum")]
        [InlineData("ValueType")]
        [InlineData("Array")]
        [InlineData("IntPtr")]
        public void ProbeExpressionParser_InstanceOf_KnownBclType_DoesNotScanAssemblies(string typeName)
        {
            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() => throw new InvalidOperationException("Assembly scanning should not run for BCL aliases."));

                object value = typeName switch
                {
                    { } name when name.IndexOf("String", StringComparison.OrdinalIgnoreCase) >= 0 => "hello",
                    { } name when name.IndexOf("Int32", StringComparison.OrdinalIgnoreCase) >= 0 || name.Equals("int", StringComparison.OrdinalIgnoreCase) => 42,
                    { } name when name.IndexOf("Guid", StringComparison.OrdinalIgnoreCase) >= 0 => Guid.NewGuid(),
                    { } name when name.IndexOf("DateTimeOffset", StringComparison.OrdinalIgnoreCase) >= 0 => DateTimeOffset.UtcNow,
                    { } name when name.IndexOf("DateTime", StringComparison.OrdinalIgnoreCase) >= 0 => DateTime.UtcNow,
                    { } name when name.IndexOf("TimeSpan", StringComparison.OrdinalIgnoreCase) >= 0 => TimeSpan.FromSeconds(1),
                    { } name when name.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0 => new Exception(),
                    { } name when name.IndexOf("Enum", StringComparison.OrdinalIgnoreCase) >= 0 => DayOfWeek.Friday,
                    { } name when name.IndexOf("ValueType", StringComparison.OrdinalIgnoreCase) >= 0 => 42,
                    { } name when name.IndexOf("Array", StringComparison.OrdinalIgnoreCase) >= 0 => Array.Empty<string>(),
                    { } name when name.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0 => typeof(string),
                    { } name when name.IndexOf("IntPtr", StringComparison.OrdinalIgnoreCase) >= 0 => new IntPtr(42),
                    _ => throw new InvalidOperationException($"Unexpected test case: {typeName}")
                };

                InstanceOfHelper.IsInstanceOf((object)value, typeName).Should().BeTrue();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Theory]
        [InlineData("System.String, mscorlib")]
        [InlineData("System.String, System.Runtime")]
        [InlineData("System.String, System.Private.CoreLib")]
        [InlineData("System.String, netstandard")]
        public void ProbeExpressionParser_InstanceOf_AssemblyQualifiedBclType_DoesNotScanAssemblies(string typeName)
        {
            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() => throw new InvalidOperationException("Assembly scanning should not run for BCL aliases."));

                InstanceOfHelper.IsInstanceOf("hello", typeName).Should().BeTrue();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_AssemblyQualifiedBclAlias_RequiresFullyQualifiedClrTypeName()
        {
            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() => throw new InvalidOperationException("Assembly scanning should not run for framework aliases."));

                Action lookup = () => InstanceOfHelper.ResolveType("string, mscorlib");

                lookup.Should().Throw<Exception>().WithMessage("*must be a fully qualified type name*");
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Theory]
        [InlineData("System.Collections.Generic.List`1[[System.String, mscorlib]], mscorlib")]
        public void ProbeExpressionParser_InstanceOf_AssemblyQualifiedFrameworkType_DoesNotScanAssemblies(string typeName)
        {
            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() => throw new InvalidOperationException("Assembly scanning should not run for framework aliases."));

                InstanceOfHelper.IsInstanceOf(new List<string>(), typeName).Should().BeTrue();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_AssemblyQualifiedFrameworkGenericTypeWithLoadedArgument_EvaluatesCondition()
        {
            var argumentAssembly = CreateDynamicAssembly(
                "InstanceOfFrameworkGenericArgumentAssembly",
                "FrameworkGenericArgument.Argument");

            try
            {
                var argumentType = argumentAssembly.GetType("FrameworkGenericArgument.Argument");
                var listType = typeof(List<>).MakeGenericType(argumentType);
                var instance = Activator.CreateInstance(listType);
                var typeName = "System.Collections.Generic.List`1[[FrameworkGenericArgument.Argument, InstanceOfFrameworkGenericArgumentAssembly]], mscorlib";
                InstanceOfHelper.SetAssemblyProviderForTests(() => [argumentAssembly]);

                InstanceOfHelper.IsInstanceOf(instance, typeName).Should().BeTrue();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_LoadedGenericTypeWithLoadedArgument_EvaluatesCondition()
        {
            var assembly = CreateDynamicAssembly(
                "InstanceOfLoadedGenericAssembly",
                "LoadedGeneric.GenericType`1",
                "LoadedGeneric.GenericArgument");

            try
            {
                var genericArgument = assembly.GetType("LoadedGeneric.GenericArgument");
                var genericType = assembly.GetType("LoadedGeneric.GenericType`1").MakeGenericType(genericArgument);
                var instance = Activator.CreateInstance(genericType);
                InstanceOfHelper.SetAssemblyProviderForTests(() => [assembly]);

                InstanceOfHelper.IsInstanceOf(instance, genericType.FullName).Should().BeTrue();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_LoadedGenericTypeWithKnownFrameworkArgument_EvaluatesCondition()
        {
            var assembly = CreateDynamicAssembly(
                "InstanceOfLoadedGenericFrameworkArgumentAssembly",
                "LoadedGenericFrameworkArgument.GenericType`1");
            var genericType = assembly.GetType("LoadedGenericFrameworkArgument.GenericType`1").MakeGenericType(typeof(string));
            var instance = Activator.CreateInstance(genericType);
            var typeName = "LoadedGenericFrameworkArgument.GenericType`1[[System.String, mscorlib]]";

            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() => [assembly]);

                InstanceOfHelper.IsInstanceOf(instance, typeName).Should().BeTrue();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_LoadedGenericTypeWithUnloadedArgument_DoesNotLoadArgumentAssembly()
        {
            var assembly = CreateDynamicAssembly(
                "InstanceOfGenericDefinitionAssembly",
                "GenericDefinition.GenericType`1");
            var unloadedArgumentAssemblyName = $"InstanceOfUnloadedGenericArgumentAssembly_{Guid.NewGuid():N}";
            var typeName = $"GenericDefinition.GenericType`1[[Unloaded.Argument, {unloadedArgumentAssemblyName}]]";

            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() => [assembly]);

                Action lookup = () => InstanceOfHelper.ResolveType(typeName);

                lookup.Should().Throw<Exception>().WithMessage("*unknown type*");
                AppDomain.CurrentDomain.GetAssemblies()
                         .Should()
                         .NotContain(a => a.GetName().Name == unloadedArgumentAssemblyName);
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_GenericArgumentAssemblyRequiresExactIdentity()
        {
            var assembly = CreateDynamicAssembly(
                "InstanceOfGenericArgumentVersionAssembly",
                "GenericArgumentVersion.GenericType`1",
                "GenericArgumentVersion.GenericArgument");
            var argumentAssemblyName = assembly.GetName().Name;
            var typeName = $"GenericArgumentVersion.GenericType`1[[GenericArgumentVersion.GenericArgument, {argumentAssemblyName}, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null]]";

            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() => [assembly]);

                Action lookup = () => InstanceOfHelper.ResolveType(typeName);

                lookup.Should().Throw<Exception>().WithMessage("*unknown type*");
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_CustomerSimpleName_ReportsRuntimeError()
        {
            try
            {
                InstanceOfHelper.ResetForTests();
                var scopeMembers = CreateScopeMembers();
                var evaluator = new ProbeExpressionEvaluator(
                    templates: null,
                    condition: new DebuggerExpression(string.Empty, CreateInstanceOfJson(@"{""ref"":""NestedObjectLocal""}", "NestedObject"), null),
                    metric: null,
                    spanDecorations: null,
                    captureExpressions: null);

                var result = evaluator.Evaluate(scopeMembers);

                result.Condition.Should().BeTrue();
                result.HasConditionError.Should().BeTrue();
                result.Errors.Should().ContainSingle();
                result.Errors[0].Message.Should().Contain("must be a fully qualified type name");
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_AssemblyQualifiedTypeName_EvaluatesCondition()
        {
            try
            {
                InstanceOfHelper.ResetForTests();
                var type = typeof(TestStruct.NestedObject);

                InstanceOfHelper.IsInstanceOf(TestObject.Nested, $"{type.FullName}, {type.Assembly.GetName().Name}").Should().BeTrue();
                InstanceOfHelper.IsInstanceOf(TestObject.Nested, type.AssemblyQualifiedName).Should().BeTrue();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_AssemblyQualifiedCustomerType_ScansLoadedAssembliesOnly()
        {
            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() => []);

                Action lookup = () => InstanceOfHelper.ResolveType("Unknown.CustomerType, UnknownCustomerAssembly");

                lookup.Should().Throw<Exception>().WithMessage("*unknown type*");
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_ResolvedCustomerType_EvaluatesTemplate()
        {
            try
            {
                InstanceOfHelper.ResetForTests();
                var scopeMembers = CreateScopeMembers();
                var typeName = typeof(TestStruct.NestedObject).FullName;
                var evaluator = new ProbeExpressionEvaluator(
                    templates: [new(null, null, "instanceof: "), new(string.Empty, CreateInstanceOfJson(@"{""ref"":""NestedObjectLocal""}", typeName), null)],
                    condition: null,
                    metric: null,
                    spanDecorations: null,
                    captureExpressions: null);

                var result = evaluator.Evaluate(scopeMembers);

                result.Template.Should().Be("instanceof: True");
                result.Errors.Should().BeNullOrEmpty();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_ResolvedCustomerType_EvaluatesCaptureExpression()
        {
            try
            {
                InstanceOfHelper.ResetForTests();
                var scopeMembers = CreateScopeMembers();
                var typeName = typeof(TestStruct.NestedObject).FullName;
                var evaluator = new ProbeExpressionEvaluator(
                    templates: null,
                    condition: null,
                    metric: null,
                    spanDecorations: null,
                    captureExpressions:
                    [
                        new CaptureExpressionDefinition("is_nested", new DebuggerExpression(string.Empty, CreateInstanceOfJson(@"{""ref"":""NestedObjectLocal""}", typeName), null), default)
                    ]);

                ExpressionEvaluationResult result = default;
                evaluator.EvaluateCaptureExpressions(ref result, scopeMembers);

                result.CaptureExpressionCount.Should().Be(1);
                result.CaptureExpressions[0].Name.Should().Be("is_nested");
                result.CaptureExpressions[0].Value.Should().Be(true);
                result.CaptureExpressions[0].Type.Should().Be(typeof(bool));
                result.Errors.Should().BeNullOrEmpty();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_ResolvedCustomerType_EvaluatesSpanDecoration()
        {
            try
            {
                InstanceOfHelper.ResetForTests();
                var scopeMembers = CreateScopeMembers();
                var typeName = typeof(TestStruct.NestedObject).FullName;
                var evaluator = new ProbeExpressionEvaluator(
                    templates: null,
                    condition: null,
                    metric: null,
                    spanDecorations:
                    [
                        new KeyValuePair<DebuggerExpression?, KeyValuePair<string, DebuggerExpression?[]>[]>(
                            new DebuggerExpression(string.Empty, CreateInstanceOfJson(@"{""ref"":""NestedObjectLocal""}", typeName), null),
                            [new KeyValuePair<string, DebuggerExpression?[]>("tag", [new DebuggerExpression(null, null, "decorated")])])
                    ],
                    captureExpressions: null);

                var result = evaluator.Evaluate(scopeMembers);

                result.Decorations.Should().ContainSingle();
                result.Decorations[0].TagName.Should().Be("tag");
                result.Decorations[0].Value.Should().Be("decorated");
                result.Decorations[0].Errors.Should().BeNullOrEmpty();
                result.Errors.Should().BeNullOrEmpty();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_UnknownType_ReportsRuntimeError()
        {
            try
            {
                InstanceOfHelper.ResetForTests();
                var scopeMembers = CreateScopeMembers();
                var evaluator = new ProbeExpressionEvaluator(
                    templates: null,
                    condition: new DebuggerExpression(string.Empty, CreateInstanceOfJson(@"{""ref"":""NestedObjectLocal""}", "DebuggerExpressionLanguageTests"), null),
                    metric: null,
                    spanDecorations: null,
                    captureExpressions: null);

                var result = evaluator.Evaluate(scopeMembers);

                result.Condition.Should().BeTrue();
                result.HasConditionError.Should().BeTrue();
                result.Errors.Should().ContainSingle();
                result.Errors[0].Message.Should().Contain("must be a fully qualified type name");
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_AmbiguousType_ReportsRuntimeError()
        {
            var firstAssembly = CreateDynamicAssembly(
                "InstanceOfAmbiguousAssemblyOne",
                "Ambiguous.TypeForInstanceOf");
            var secondAssembly = CreateDynamicAssembly(
                "InstanceOfAmbiguousAssemblyTwo",
                "Ambiguous.TypeForInstanceOf");

            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() => [firstAssembly, secondAssembly]);

                Action lookup = () => InstanceOfHelper.ResolveType("Ambiguous.TypeForInstanceOf");

                lookup.Should().Throw<Exception>().WithMessage("*Multiple types matching*");
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_AssemblyInspectionFailure_ContinuesScanning()
        {
            var throwingAssembly = new ThrowingGetTypeAssembly("InstanceOfThrowingAssembly");
            var resolvedAssembly = CreateDynamicAssembly(
                "InstanceOfInspectionFailureResolvedAssembly",
                "InspectionFailure.TypeForInstanceOf");

            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() => [throwingAssembly, resolvedAssembly]);

                InstanceOfHelper.ResolveType("InspectionFailure.TypeForInstanceOf").Should().Be(resolvedAssembly.GetType("InspectionFailure.TypeForInstanceOf"));
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_ExactCaseMatch_ResolvesCasingAmbiguousType()
        {
            var assembly = CreateDynamicAssembly(
                "InstanceOfExactCaseAmbiguousAssembly",
                "ExactCase.TypeForInstanceOf",
                "ExactCase.typeforinstanceof");

            try
            {
                var exactType = assembly.GetType("ExactCase.TypeForInstanceOf");
                var casingVariantType = assembly.GetType("ExactCase.typeforinstanceof");
                casingVariantType.Should().NotBe(exactType);

                var instance = Activator.CreateInstance(exactType);
                var casingVariantInstance = Activator.CreateInstance(casingVariantType);
                InstanceOfHelper.SetAssemblyProviderForTests(() => [assembly]);

                InstanceOfHelper.IsInstanceOf(instance, "ExactCase.TypeForInstanceOf").Should().BeTrue();
                InstanceOfHelper.IsInstanceOf(casingVariantInstance, "ExactCase.TypeForInstanceOf").Should().BeFalse();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_ExactCaseMatchAcrossAssemblies_ResolvesExactMatch()
        {
            var firstAssembly = CreateDynamicAssembly(
                "InstanceOfExactCaseAcrossAssembliesVariantAssembly",
                "ExactCaseAcrossAssemblies.typeforinstanceof");
            var secondAssembly = CreateDynamicAssembly(
                "InstanceOfExactCaseAcrossAssembliesExactAssembly",
                "ExactCaseAcrossAssemblies.TypeForInstanceOf");

            try
            {
                var casingVariantType = firstAssembly.GetType("ExactCaseAcrossAssemblies.typeforinstanceof");
                var exactType = secondAssembly.GetType("ExactCaseAcrossAssemblies.TypeForInstanceOf");
                casingVariantType.Should().NotBe(exactType);

                var instance = Activator.CreateInstance(exactType);
                var casingVariantInstance = Activator.CreateInstance(casingVariantType);
                InstanceOfHelper.SetAssemblyProviderForTests(() => [firstAssembly, secondAssembly]);

                InstanceOfHelper.IsInstanceOf(instance, "ExactCaseAcrossAssemblies.TypeForInstanceOf").Should().BeTrue();
                InstanceOfHelper.IsInstanceOf(casingVariantInstance, "ExactCaseAcrossAssemblies.TypeForInstanceOf").Should().BeFalse();
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_AssemblyQualifiedTypeNameRequiresExactCasing()
        {
            var assembly = CreateDynamicAssembly(
                "InstanceOfAssemblyQualifiedExactCaseAssembly",
                "AssemblyQualifiedExactCase.TypeForInstanceOf");
            var typeName = "AssemblyQualifiedExactCase.typeforinstanceof, InstanceOfAssemblyQualifiedExactCaseAssembly";

            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() => [assembly]);

                Action lookup = () => InstanceOfHelper.ResolveType(typeName);

                lookup.Should().Throw<Exception>().WithMessage("*unknown type*");
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_AmbiguousTypeAfterCacheHit_KeepsCachedResolution()
        {
            var firstAssembly = CreateDynamicAssembly(
                "InstanceOfAmbiguousCachedAssemblyOne",
                "AmbiguousCached.TypeForInstanceOf");
            var secondAssembly = CreateDynamicAssembly(
                "InstanceOfAmbiguousCachedAssemblyTwo",
                "AmbiguousCached.TypeForInstanceOf");
            var calls = 0;

            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() =>
                {
                    calls++;
                    return calls == 1 ? [firstAssembly] : [firstAssembly, secondAssembly];
                });

                InstanceOfHelper.ResolveType("AmbiguousCached.TypeForInstanceOf").Should().Be(firstAssembly.GetType("AmbiguousCached.TypeForInstanceOf"));
                InstanceOfHelper.IncrementAssemblyLoadGenerationForTests();

                InstanceOfHelper.ResolveType("AmbiguousCached.TypeForInstanceOf").Should().Be(firstAssembly.GetType("AmbiguousCached.TypeForInstanceOf"));
                calls.Should().Be(1);
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_LazyLoadedType_CanSucceedWithoutRecompile()
        {
            var firstAssembly = typeof(string).Assembly;
            var secondAssembly = CreateDynamicAssembly(
                "InstanceOfLazyAssembly",
                "LazyLoaded.TypeForInstanceOf");
            var includeSecondAssembly = false;
            var calls = 0;

            try
            {
                // Gate the second assembly on an explicit flag rather than the call count: an unrelated
                // assembly load can bump the generation and make ResolveType retry, so the provider may be
                // invoked more than once per lookup.
                InstanceOfHelper.SetAssemblyProviderForTests(() =>
                {
                    calls++;
                    return includeSecondAssembly ? [firstAssembly, secondAssembly] : [firstAssembly];
                });

                var instance = Activator.CreateInstance(secondAssembly.GetType("LazyLoaded.TypeForInstanceOf"));

                Action firstLookup = () => InstanceOfHelper.ResolveType("LazyLoaded.TypeForInstanceOf");
                firstLookup.Should().Throw<Exception>().WithMessage("*unknown type*");
                var callsAfterFirstLookup = calls;

                includeSecondAssembly = true;
                InstanceOfHelper.IncrementAssemblyLoadGenerationForTests();
                InstanceOfHelper.IsInstanceOf(instance, "LazyLoaded.TypeForInstanceOf").Should().BeTrue();
                calls.Should().BeGreaterThan(callsAfterFirstLookup);
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_LazyLoadedGenericArgument_CanSucceedWithoutRecompile()
        {
            var outerAssembly = CreateDynamicAssembly(
                "InstanceOfLazyGenericOuterAssembly",
                "LazyGeneric.Outer`1");
            var argumentAssembly = CreateDynamicAssembly(
                "InstanceOfLazyGenericArgumentAssembly",
                "LazyGeneric.Argument");
            var includeArgumentAssembly = false;
            var calls = 0;

            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() =>
                {
                    calls++;
                    return includeArgumentAssembly ? [outerAssembly, argumentAssembly] : [outerAssembly];
                });

                var typeName = "LazyGeneric.Outer`1[[LazyGeneric.Argument, InstanceOfLazyGenericArgumentAssembly]]";

                Action firstLookup = () => InstanceOfHelper.ResolveType(typeName);
                firstLookup.Should().Throw<Exception>().WithMessage("*unknown type*");

                InstanceOfHelper.IncrementAssemblyLoadGenerationForTests();
                includeArgumentAssembly = true;
                var argumentType = argumentAssembly.GetType("LazyGeneric.Argument");
                var genericType = outerAssembly.GetType("LazyGeneric.Outer`1").MakeGenericType(argumentType);
                var instance = Activator.CreateInstance(genericType);
                InstanceOfHelper.IsInstanceOf(instance, typeName).Should().BeTrue();
                calls.Should().BeGreaterThan(1);
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_CacheHit_DoesNotScanAssembliesAgain()
        {
            var assembly = typeof(TestStruct.NestedObject).Assembly;
            var calls = 0;

            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() =>
                {
                    calls++;
                    return [assembly];
                });

                var typeName = typeof(TestStruct.NestedObject).FullName;
                InstanceOfHelper.IsInstanceOf(TestObject.Nested, typeName).Should().BeTrue();
                InstanceOfHelper.IsInstanceOf(TestObject.Nested, typeName).Should().BeTrue();

                calls.Should().Be(1);
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_MissScansOnlyNewAssemblies()
        {
            var firstAssembly = typeof(string).Assembly;
            var secondAssembly = typeof(TestStruct.NestedObject).Assembly;
            var includeSecondAssembly = false;
            var calls = 0;

            try
            {
                // Gate the second assembly on an explicit flag rather than the call count: an unrelated
                // assembly load can bump the generation and make ResolveType retry, so the provider may be
                // invoked more than once per lookup.
                InstanceOfHelper.SetAssemblyProviderForTests(() =>
                {
                    calls++;
                    return includeSecondAssembly ? [firstAssembly, secondAssembly] : [firstAssembly];
                });

                Action firstLookup = () => InstanceOfHelper.ResolveType("Missing.TypeForInstanceOf");
                firstLookup.Should().Throw<Exception>().WithMessage("*unknown type*");
                var callsAfterFirstLookup = calls;

                includeSecondAssembly = true;
                InstanceOfHelper.IncrementAssemblyLoadGenerationForTests();
                Action secondLookup = () => InstanceOfHelper.ResolveType("Missing.TypeForInstanceOf");
                secondLookup.Should().Throw<Exception>().WithMessage("*unknown type*");

                // Assert the delta rather than an absolute count: the second lookup must rescan
                // (re-invoke the provider) after the new assembly appears rather than serve the cached
                // miss. Both lookups throw *unknown type*, so an unrelated retry inflating the first
                // lookup's count must not be able to satisfy this on its own.
                calls.Should().BeGreaterThan(callsAfterFirstLookup);
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_LargeMissCacheScansOnlyNewAssemblies()
        {
            var initialAssemblies = new Assembly[9];
            for (var i = 0; i < initialAssemblies.Length; i++)
            {
                initialAssemblies[i] = CreateDynamicAssembly(
                    $"InstanceOfLargeMissCacheAssembly{i}",
                    $"LargeMissCache.IgnoredType{i}");
            }

            var resolvedAssembly = CreateDynamicAssembly(
                "InstanceOfLargeMissCacheResolvedAssembly",
                "LargeMissCache.ResolvedType");
            var includeResolvedAssembly = false;
            var calls = 0;

            try
            {
                // Gate the resolved assembly on an explicit flag rather than the call count: an unrelated
                // assembly load can bump the generation and make ResolveType retry, so the provider may be
                // invoked more than once per lookup.
                InstanceOfHelper.SetAssemblyProviderForTests(() =>
                {
                    calls++;
                    return includeResolvedAssembly ? [.. initialAssemblies, resolvedAssembly] : initialAssemblies;
                });

                Action firstLookup = () => InstanceOfHelper.ResolveType("LargeMissCache.ResolvedType");
                firstLookup.Should().Throw<Exception>().WithMessage("*unknown type*");
                var callsAfterFirstLookup = calls;

                includeResolvedAssembly = true;
                InstanceOfHelper.IncrementAssemblyLoadGenerationForTests();
                InstanceOfHelper.ResolveType("LargeMissCache.ResolvedType").Should().Be(resolvedAssembly.GetType("LargeMissCache.ResolvedType"));

                calls.Should().BeGreaterThan(callsAfterFirstLookup);
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_CacheClearDuringMerge_PreservesScannedSnapshot()
        {
            var assembly = CreateDynamicAssembly(
                "InstanceOfCacheClearAssembly",
                "CacheClear.TypeForInstanceOf");

            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() => [assembly]);

                InstanceOfHelper.ResolveType("CacheClear.TypeForInstanceOf").Should().Be(assembly.GetType("CacheClear.TypeForInstanceOf"));

                for (var i = 0; i < 511; i++)
                {
                    Action fillCache = () => InstanceOfHelper.ResolveType($"Missing.CacheFill{i}");
                    fillCache.Should().Throw<Exception>().WithMessage("*unknown type*");
                }

                InstanceOfHelper.IncrementAssemblyLoadGenerationForTests();

                InstanceOfHelper.ResolveType("CacheClear.TypeForInstanceOf").Should().Be(assembly.GetType("CacheClear.TypeForInstanceOf"));
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Fact]
        public void ProbeExpressionParser_InstanceOf_ContinuousAssemblyLoadChurn_StopsAfterBoundedRetries()
        {
            var calls = 0;

            try
            {
                InstanceOfHelper.SetAssemblyProviderForTests(() =>
                {
                    calls++;
                    InstanceOfHelper.IncrementAssemblyLoadGenerationForTests();
                    return [typeof(string).Assembly];
                });

                Action lookup = () => InstanceOfHelper.ResolveType("Missing.ChurningTypeForInstanceOf");

                lookup.Should().Throw<Exception>().WithMessage("*unknown type*");
                calls.Should().Be(3);
            }
            finally
            {
                InstanceOfHelper.ResetForTests();
            }
        }

        [Theory]
        [MemberData(nameof(SupportedSensitiveDictionaries))]
        public void ProbeExpressionParser_DictionaryIteratorValue_RedactsSensitiveKeys(object dictionary)
        {
            var scopeMembers = CreateScopeMembers();
            const string secret = "TOP_SECRET_VALUE";
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                dictionary.GetType(),
                dictionary,
                ScopeMemberKind.Local));

            const string json = """
                                {
                                  "getmember": [
                                    {
                                      "index": [
                                        {
                                          "filter": [
                                            { "ref": "SafeDictionaryLocal" },
                                            { "eq": [ { "ref": "@key" }, "password" ] }
                                          ]
                                        },
                                        0
                                      ]
                                    },
                                    "Value"
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.NotEqual(secret, result);
            Assert.Equal("{REDACTED}", result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Theory]
        [MemberData(nameof(SupportedSensitiveDictionaries))]
        public void ProbeExpressionParser_DictionaryIteratorEntry_RedactsSensitiveKeys(object dictionary)
        {
            var scopeMembers = CreateScopeMembers();
            const string secret = "TOP_SECRET_VALUE";
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                dictionary.GetType(),
                dictionary,
                ScopeMemberKind.Local));

            const string json = """
                                {
                                  "index": [
                                    {
                                      "filter": [
                                        { "ref": "SafeDictionaryLocal" },
                                        { "eq": [ { "ref": "@key" }, "password" ] }
                                      ]
                                    },
                                    0
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.NotEqual(secret, result);
            Assert.Equal("{REDACTED}", result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_DictionaryIteratorValue_DoesNotCallUnsafeKeyToString()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Dictionary<ThrowsOnToStringKey, string>),
                new Dictionary<ThrowsOnToStringKey, string>
                {
                    { new ThrowsOnToStringKey(), "hello" },
                },
                ScopeMemberKind.Local));

            const string json = """
                                {
                                  "any": [
                                    { "ref": "SafeDictionaryLocal" },
                                    { "eq": [ "@value", "hello" ] }
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<bool>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_DictionaryIteratorValue_UsesSafeObjectEquality()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Hashtable),
                new Hashtable
                {
                    { "public", "hello" },
                },
                ScopeMemberKind.Local));

            const string json = """
                                {
                                  "any": [
                                    { "ref": "SafeDictionaryLocal" },
                                    { "eq": [ "@value", "hello" ] }
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<bool>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_DictionaryIteratorValue_DoesNotCallUnsafeObjectEquals()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Hashtable),
                new Hashtable
                {
                    { "public", new ThrowsOnEqualsValue() },
                },
                ScopeMemberKind.Local));

            const string json = """
                                {
                                  "any": [
                                    { "ref": "SafeDictionaryLocal" },
                                    { "eq": [ "@value", "public" ] }
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<bool>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0, string.Join(Environment.NewLine, compiled.Errors?.Select(e => $"{e.Expression}: {e.Message}") ?? []));
            Assert.False(result);
        }

        [Fact]
        public void ProbeExpressionParser_DictionaryIteratorValue_RedactsStringPredicatesWithSensitiveKeys()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Dictionary<string, string>),
                new Dictionary<string, string>
                {
                    { "password", "hello" },
                    { "public", "hello" },
                },
                ScopeMemberKind.Local));

            const string publicJson = """
                                      {
                                        "any": [
                                          {
                                            "filter": [
                                              { "ref": "SafeDictionaryLocal" },
                                              { "eq": [ { "ref": "@key" }, "public" ] }
                                            ]
                                          },
                                          { "eq": [ "@value", "hello" ] }
                                        ]
                                      }
                                      """;

            const string sensitiveJson = """
                                         {
                                           "any": [
                                             {
                                               "filter": [
                                                 { "ref": "SafeDictionaryLocal" },
                                                 { "eq": [ { "ref": "@key" }, "password" ] }
                                               ]
                                             },
                                             { "eq": [ "@value", "hello" ] }
                                           ]
                                         }
                                         """;

            var publicCompiled = ProbeExpressionParser<bool>.ParseExpression(publicJson, scopeMembers);
            var publicResult = publicCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(publicResult);
            Assert.True(publicCompiled.Errors == null || publicCompiled.Errors.Length == 0);

            var sensitiveCompiled = ProbeExpressionParser<bool>.ParseExpression(sensitiveJson, scopeMembers);
            var sensitiveResult = sensitiveCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.False(sensitiveResult);
            Assert.True(sensitiveCompiled.Errors == null || sensitiveCompiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_DictionaryIteratorValue_PreservesNumericValueTypeForComparisons()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Dictionary<string, int>),
                new Dictionary<string, int>
                {
                    { "password", 42 },
                    { "public", 42 },
                },
                ScopeMemberKind.Local));

            const string publicJson = """
                                      {
                                        "any": [
                                          {
                                            "filter": [
                                              { "ref": "SafeDictionaryLocal" },
                                              { "eq": [ { "ref": "@key" }, "public" ] }
                                            ]
                                          },
                                          { "gt": [ "@value", 10 ] }
                                        ]
                                      }
                                      """;

            const string sensitiveJson = """
                                         {
                                           "any": [
                                             {
                                               "filter": [
                                                 { "ref": "SafeDictionaryLocal" },
                                                 { "eq": [ { "ref": "@key" }, "password" ] }
                                               ]
                                             },
                                             { "gt": [ "@value", 10 ] }
                                           ]
                                         }
                                         """;

            var publicCompiled = ProbeExpressionParser<bool>.ParseExpression(publicJson, scopeMembers);
            var publicResult = publicCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(publicResult);
            Assert.True(publicCompiled.Errors == null || publicCompiled.Errors.Length == 0);

            var sensitiveCompiled = ProbeExpressionParser<bool>.ParseExpression(sensitiveJson, scopeMembers);
            var sensitiveResult = sensitiveCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.False(sensitiveResult);
            Assert.True(sensitiveCompiled.Errors == null || sensitiveCompiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_DictionaryIteratorValue_PreservesRedactionAfterNumericConversion()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Dictionary<string, int>),
                new Dictionary<string, int>
                {
                    { "password", 42 },
                    { "public", 42 },
                },
                ScopeMemberKind.Local));

            const string publicJson = """
                                      {
                                        "any": [
                                          {
                                            "filter": [
                                              { "ref": "SafeDictionaryLocal" },
                                              { "eq": [ { "ref": "@key" }, "public" ] }
                                            ]
                                          },
                                          { "gt": [ "@value", 10.5 ] }
                                        ]
                                      }
                                      """;

            const string sensitiveJson = """
                                         {
                                           "any": [
                                             {
                                               "filter": [
                                                 { "ref": "SafeDictionaryLocal" },
                                                 { "eq": [ { "ref": "@key" }, "password" ] }
                                               ]
                                             },
                                             { "gt": [ "@value", 10.5 ] }
                                           ]
                                         }
                                         """;

            var publicCompiled = ProbeExpressionParser<bool>.ParseExpression(publicJson, scopeMembers);
            var publicResult = publicCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(publicResult);
            Assert.True(publicCompiled.Errors == null || publicCompiled.Errors.Length == 0);

            var sensitiveCompiled = ProbeExpressionParser<bool>.ParseExpression(sensitiveJson, scopeMembers);
            var sensitiveResult = sensitiveCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.False(sensitiveResult);
            Assert.True(sensitiveCompiled.Errors == null || sensitiveCompiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_DictionaryIteratorValue_RedactsNestedValuesBeforeMemberAccess()
        {
            var scopeMembers = CreateScopeMembers();
            const string secret = "TOP_SECRET_VALUE";
            const string publicValue = "hello";
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Dictionary<string, SensitiveValueHolder>),
                new Dictionary<string, SensitiveValueHolder>
                {
                    { "password", new SensitiveValueHolder { Data = secret } },
                    { "public", new SensitiveValueHolder { Data = publicValue } },
                },
                ScopeMemberKind.Local));

            const string sensitiveJson = """
                                {
                                  "getmember": [
                                    {
                                      "getmember": [
                                        {
                                          "index": [
                                            {
                                              "filter": [
                                                { "ref": "SafeDictionaryLocal" },
                                                { "eq": [ { "ref": "@key" }, "password" ] }
                                              ]
                                            },
                                            0
                                          ]
                                        },
                                        "Value"
                                      ]
                                    },
                                    "Data"
                                  ]
                                }
                                """;

            const string publicJson = """
                                      {
                                        "getmember": [
                                          {
                                            "getmember": [
                                              {
                                                "index": [
                                                  {
                                                    "filter": [
                                                      { "ref": "SafeDictionaryLocal" },
                                                      { "eq": [ { "ref": "@key" }, "public" ] }
                                                    ]
                                                  },
                                                  0
                                                ]
                                              },
                                              "Value"
                                            ]
                                          },
                                          "Data"
                                        ]
                                      }
                                      """;

            var sensitiveCompiled = ProbeExpressionParser<object>.ParseExpression(sensitiveJson, scopeMembers);
            var sensitiveResult = sensitiveCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.NotEqual(secret, sensitiveResult);
            Assert.Equal("{REDACTED}", sensitiveResult);

            var publicCompiled = ProbeExpressionParser<object>.ParseExpression(publicJson, scopeMembers);
            var publicResult = publicCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Equal(publicValue, publicResult);
        }

        [Fact]
        public void ProbeExpressionParser_PrivateAutoPropertyBackingFieldExpression_CompilesAndExecutes()
        {
            var target = new AutoPropertyTarget { Value = "hello" };
            var backingField = typeof(AutoPropertyTarget).GetField("<Value>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            backingField.Should().NotBeNull();

            var source = System.Linq.Expressions.Expression.Parameter(typeof(AutoPropertyTarget), "source");
            var field = System.Linq.Expressions.Expression.Field(source, backingField);
            var compiled = System.Linq.Expressions.Expression.Lambda<Func<AutoPropertyTarget, string>>(field, source).Compile();

            compiled(target).Should().Be("hello");
        }

        [Fact]
        public void ProbeExpressionParser_GetMember_AutoPropertyReadsBackingField()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember("TargetLocal", typeof(AutoPropertyTarget), new AutoPropertyTarget { Value = "hello" }, ScopeMemberKind.Local));

            const string json = """
                                {
                                  "getmember": [
                                    {
                                      "ref": "TargetLocal"
                                    },
                                    "Value"
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<string>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Equal("hello", result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_GetMember_InheritedAutoPropertyReadsBaseBackingField()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember("TargetLocal", typeof(DerivedAutoPropertyTarget), new DerivedAutoPropertyTarget { BaseValue = "base-value" }, ScopeMemberKind.Local));

            const string json = """
                                {
                                  "getmember": [
                                    {
                                      "ref": "TargetLocal"
                                    },
                                    "BaseValue"
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<string>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Equal("base-value", result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_GetMember_DoesNotInvokeSideEffectingPropertyGetter()
        {
            SideEffectingPropertyTarget.Reset();
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember("TargetLocal", typeof(SideEffectingPropertyTarget), new SideEffectingPropertyTarget(), ScopeMemberKind.Local));

            const string json = """
                                {
                                  "getmember": [
                                    {
                                      "ref": "TargetLocal"
                                    },
                                    "UnsafeValue"
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Same(UndefinedValue.Instance, result);
            Assert.Equal(0, SideEffectingPropertyTarget.GetterCalls);
            var error = Assert.Single(compiled.Errors);
            Assert.Contains("cannot be safely read", error.Message);
        }

        [Fact]
        public void ProbeExpressionParser_GetReference_DoesNotInvokeThisPropertyGetter()
        {
            SideEffectingPropertyTarget.Reset();
            var scopeMembers = CreateScopeMembers();
            scopeMembers.InvocationTarget = new ScopeMember("this", typeof(SideEffectingPropertyTarget), new SideEffectingPropertyTarget(), ScopeMemberKind.This);

            const string json = """
                                {
                                  "ref": "UnsafeValue"
                                }
                                """;

            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Same(UndefinedValue.Instance, result);
            Assert.Equal(0, SideEffectingPropertyTarget.GetterCalls);
            var error = Assert.Single(compiled.Errors);
            Assert.Contains("cannot be safely read", error.Message);
        }

        [Fact]
        public void ProbeExpressionParser_GetMember_StaticAutoPropertyDoesNotInvokeGetterOnTypeWithCctor()
        {
            _staticExpressionInitializedCount = 0;
            var scopeMembers = CreateScopeMembers();
            scopeMembers.InvocationTarget = new ScopeMember("this", typeof(StaticExpressionWithCctor), null, ScopeMemberKind.This);

            const string json = """
                                {
                                  "getmember": [
                                    {
                                      "ref": "this"
                                    },
                                    "StaticProperty"
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Same(UndefinedValue.Instance, result);
            Assert.Equal(0, _staticExpressionInitializedCount);
            var error = Assert.Single(compiled.Errors);
            Assert.Contains("could trigger the declaring type initializer", error.Message);
        }

        [Fact]
        public void ProbeExpressionParser_DictionaryIteratorValueMember_UsesSafeResolverAndPreservesRedaction()
        {
            var scopeMembers = CreateScopeMembers();
            const string secret = "TOP_SECRET_VALUE";
            const string publicValue = "hello";
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Dictionary<string, SideEffectingValueHolder>),
                new Dictionary<string, SideEffectingValueHolder>
                {
                    { "password", new SideEffectingValueHolder(secret) },
                    { "public", new SideEffectingValueHolder(publicValue) },
                },
                ScopeMemberKind.Local));

            const string sensitiveJson = """
                                {
                                  "getmember": [
                                    {
                                      "getmember": [
                                        {
                                          "index": [
                                            {
                                              "filter": [
                                                { "ref": "SafeDictionaryLocal" },
                                                { "eq": [ { "ref": "@key" }, "password" ] }
                                              ]
                                            },
                                            0
                                          ]
                                        },
                                        "Value"
                                      ]
                                    },
                                    "Data"
                                  ]
                                }
                                """;

            const string publicJson = """
                                      {
                                        "getmember": [
                                          {
                                            "getmember": [
                                              {
                                                "index": [
                                                  {
                                                    "filter": [
                                                      { "ref": "SafeDictionaryLocal" },
                                                      { "eq": [ { "ref": "@key" }, "public" ] }
                                                    ]
                                                  },
                                                  0
                                                ]
                                              },
                                              "Value"
                                            ]
                                          },
                                          "Data"
                                        ]
                                      }
                                      """;

            SideEffectingValueHolder.Reset();
            var sensitiveCompiled = ProbeExpressionParser<object>.ParseExpression(sensitiveJson, scopeMembers);
            var sensitiveResult = sensitiveCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            var publicCompiled = ProbeExpressionParser<object>.ParseExpression(publicJson, scopeMembers);
            var publicResult = publicCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Equal("{REDACTED}", sensitiveResult);
            Assert.Equal(publicValue, publicResult);
            Assert.Equal(0, SideEffectingValueHolder.GetterCalls);
            Assert.True(sensitiveCompiled.Errors == null || sensitiveCompiled.Errors.Length == 0);
            Assert.True(publicCompiled.Errors == null || publicCompiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_DictionaryIteratorEntry_CanUseExplicitKeyMemberInPredicate()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Dictionary<string, string>),
                new Dictionary<string, string>
                {
                    { "hello", "world" },
                    { "goodbye", "moon" },
                },
                ScopeMemberKind.Local));

            const string json = """
                                {
                                  "any": [
                                    {
                                      "ref": "SafeDictionaryLocal"
                                    },
                                    {
                                      "eq": [
                                        {
                                          "getmember": [
                                            {
                                              "ref": "@it"
                                            },
                                            "Key"
                                          ]
                                        },
                                        "missing"
                                      ]
                                    }
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<bool>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.False(result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_DictionaryIteratorEntry_CanUseExplicitKeyMemberAfterIndex()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Dictionary<string, string>),
                new Dictionary<string, string>
                {
                    { "hello", "world" },
                    { "goodbye", "moon" },
                },
                ScopeMemberKind.Local));

            const string json = """
                                {
                                  "getmember": [
                                    {
                                      "index": [
                                        {
                                          "filter": [
                                            { "ref": "SafeDictionaryLocal" },
                                            { "eq": [ { "ref": "@key" }, "goodbye" ] }
                                          ]
                                        },
                                        0
                                      ]
                                    },
                                    "Key"
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<string>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Equal("goodbye", result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_DictionaryIteratorValueMember_RedactsUnsafeMemberAccess()
        {
            var scopeMembers = CreateScopeMembers();
            const string secret = "TOP_SECRET_VALUE";
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Dictionary<string, SideEffectingValueHolder>),
                new Dictionary<string, SideEffectingValueHolder>
                {
                    { "password", new SideEffectingValueHolder(secret) },
                },
                ScopeMemberKind.Local));

            const string json = """
                                {
                                  "getmember": [
                                    {
                                      "getmember": [
                                        {
                                          "index": [
                                            {
                                              "filter": [
                                                { "ref": "SafeDictionaryLocal" },
                                                { "eq": [ { "ref": "@key" }, "password" ] }
                                              ]
                                            },
                                            0
                                          ]
                                        },
                                        "Value"
                                      ]
                                    },
                                    "UnsafeData"
                                  ]
                                }
                                """;

            SideEffectingValueHolder.Reset();
            var objectCompiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers);
            var objectResult = objectCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            var stringCompiled = ProbeExpressionParser<string>.ParseExpression(json, scopeMembers);
            var stringResult = stringCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Equal("{REDACTED}", objectResult);
            Assert.Equal("{REDACTED}", stringResult);
            Assert.Equal(0, SideEffectingValueHolder.GetterCalls);
            var objectError = Assert.Single(objectCompiled.Errors);
            Assert.Contains("cannot be safely read", objectError.Message);
            var stringError = Assert.Single(stringCompiled.Errors);
            Assert.Contains("cannot be safely read", stringError.Message);
        }

        [Fact]
        public void ProbeExpressionParser_DictionaryIteratorValueMember_SuppressesUnsafeMemberConditionForSensitiveKey()
        {
            var scopeMembers = CreateScopeMembers();
            const string secret = "TOP_SECRET_VALUE";
            const string publicValue = "hello";
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Dictionary<string, SideEffectingValueHolder>),
                new Dictionary<string, SideEffectingValueHolder>
                {
                    { "password", new SideEffectingValueHolder(secret) },
                    { "public", new SideEffectingValueHolder(publicValue) },
                },
                ScopeMemberKind.Local));

            const string sensitiveJson = """
                                         {
                                           "eq": [
                                             {
                                               "getmember": [
                                                 {
                                                   "getmember": [
                                                     {
                                                       "index": [
                                                         {
                                                           "filter": [
                                                             { "ref": "SafeDictionaryLocal" },
                                                             { "eq": [ { "ref": "@key" }, "password" ] }
                                                           ]
                                                         },
                                                         0
                                                       ]
                                                     },
                                                     "Value"
                                                   ]
                                                 },
                                                 "UnsafeData"
                                               ]
                                             },
                                             "TOP_SECRET_VALUE"
                                           ]
                                         }
                                         """;

            const string publicJson = """
                                      {
                                        "eq": [
                                          {
                                            "getmember": [
                                              {
                                                "getmember": [
                                                  {
                                                    "index": [
                                                      {
                                                        "filter": [
                                                          { "ref": "SafeDictionaryLocal" },
                                                          { "eq": [ { "ref": "@key" }, "public" ] }
                                                        ]
                                                      },
                                                      0
                                                    ]
                                                  },
                                                  "Value"
                                                ]
                                              },
                                              "UnsafeData"
                                            ]
                                          },
                                          "hello"
                                        ]
                                      }
                                      """;

            SideEffectingValueHolder.Reset();
            var sensitiveCompiled = ProbeExpressionParser<bool>.ParseExpression(sensitiveJson, scopeMembers);
            var sensitiveResult = sensitiveCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            var publicCompiled = ProbeExpressionParser<bool>.ParseExpression(publicJson, scopeMembers);
            var publicResult = publicCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.False(sensitiveResult);
            Assert.True(publicResult);
            Assert.Equal(0, SideEffectingValueHolder.GetterCalls);
            var sensitiveError = Assert.Single(sensitiveCompiled.Errors);
            Assert.Contains("cannot be safely read", sensitiveError.Message);
            var publicError = Assert.Single(publicCompiled.Errors);
            Assert.Contains("cannot be safely read", publicError.Message);
        }

        [Theory]
        [MemberData(nameof(SensitiveDictionaryValueOperations))]
        public void ProbeExpressionParser_DictionaryIteratorValue_RedactsSensitiveKeysBeforeDerivedOperations(string operationJson)
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember(
                "SafeDictionaryLocal",
                typeof(Dictionary<string, string>),
                new Dictionary<string, string>
                {
                    { "password", "TOP_SECRET_VALUE" },
                    { "public", "TOP_SECRET_VALUE" },
                },
                ScopeMemberKind.Local));

            var publicJson = $$"""
                               {
                                 "any": [
                                   {
                                     "filter": [
                                       { "ref": "SafeDictionaryLocal" },
                                       { "eq": [ { "ref": "@key" }, "public" ] }
                                     ]
                                   },
                                   {{operationJson}}
                                 ]
                               }
                               """;

            var sensitiveJson = $$"""
                                  {
                                    "any": [
                                      {
                                        "filter": [
                                          { "ref": "SafeDictionaryLocal" },
                                          { "eq": [ { "ref": "@key" }, "password" ] }
                                        ]
                                      },
                                      {{operationJson}}
                                    ]
                                  }
                                  """;

            var publicCompiled = ProbeExpressionParser<bool>.ParseExpression(publicJson, scopeMembers);
            var publicResult = publicCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(publicResult);
            Assert.True(publicCompiled.Errors == null || publicCompiled.Errors.Length == 0);

            var sensitiveCompiled = ProbeExpressionParser<bool>.ParseExpression(sensitiveJson, scopeMembers);
            var sensitiveResult = sensitiveCompiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.False(sensitiveResult);
            Assert.True(sensitiveCompiled.Errors == null || sensitiveCompiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_ConstructedOpenGenericReferenceTypeParameter_CanCompileExpression()
        {
            var scopeMembers = CreateScopeMembers();
            var collection = new List<string> { "value" };
            var openCollectionType = typeof(GenericReferenceTypeTarget<>).GetProperty(nameof(GenericReferenceTypeTarget<string>.Collection)).PropertyType;
            scopeMembers.Return = new ScopeMember("return", openCollectionType, collection, ScopeMemberKind.Return);

            const string json = """
                                {
                                  "ref": "@return"
                                }
                                """;

            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Same(collection, result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_NullConstructedOpenGenericArgument_CanCompileExpression()
        {
            var scopeMembers = CreateScopeMembers();
            var openCollectionType = typeof(GenericReferenceTypeTarget<>).GetProperty(nameof(GenericReferenceTypeTarget<string>.Collection)).PropertyType;
            scopeMembers.AddMember(new ScopeMember("OpenCollectionArg", openCollectionType, null, ScopeMemberKind.Argument));

            const string json = """
                                {
                                  "eq": [
                                    { "ref": "OpenCollectionArg" },
                                    null
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<bool>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_OpenGenericValueTypeParameter_ReturnsEvaluationError()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.InvocationTarget = new ScopeMember("this", typeof(GenericValueTypeTarget<>), null, ScopeMemberKind.This);

            const string json = """
                                {
                                  "ref": "this"
                                }
                                """;

            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers, typeof(GenericValueTypeTarget<>));
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Same(UndefinedValue.Instance, result);
            var error = Assert.Single(compiled.Errors);
            Assert.Contains("generic value type parameter", error.Message);
        }

        [Fact]
        public void ProbeExpressionParser_RecursiveGenericConstraint_ReturnsEvaluationError()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.InvocationTarget = new ScopeMember("this", typeof(RecursiveGenericConstraintTarget<>), null, ScopeMemberKind.This);

            const string json = """
                                {
                                  "ref": "this"
                                }
                                """;

            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers, typeof(RecursiveGenericConstraintTarget<>));
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Same(UndefinedValue.Instance, result);
            var error = Assert.Single(compiled.Errors);
            Assert.Contains("recursive generic parameter constraints", error.Message);
        }

        [Fact]
        public void ProbeExpressionParser_ValueTypeReferenceTypeComparison_ReturnsFriendlyError()
        {
            var scopeMembers = CreateScopeMembers();

            const string json = """
                                {
                                  "gt": [
                                    { "ref": "IntLocal" },
                                    "value"
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<bool>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(result);
            var error = Assert.Single(compiled.Errors);
            Assert.Equal("A reference type cannot be compared to a not nullable value type.", error.Message);
        }

        [Fact]
        public void ProbeExpressionParser_NullableValueTypeComparedToNull_DoesNotThrow()
        {
            var scopeMembers = CreateScopeMembers();

            const string json = """
                                {
                                  "eq": [
                                    { "ref": "NullableNullValueLocal" },
                                    null
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<bool>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_NullableValueTypeReferenceTypeComparison_KeepsOriginalError()
        {
            var scopeMembers = CreateScopeMembers();

            const string json = """
                                {
                                  "eq": [
                                    { "ref": "NullableNullValueLocal" },
                                    "value"
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<bool>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(result);
            var error = Assert.Single(compiled.Errors);
            Assert.Contains("The binary operator Equal is not defined for the types", error.Message);
        }

        [Theory]
        [InlineData("""
                    {
                      "any": [
                        {
                          "ref": "HashtableLocal"
                        },
                        {
                          "and": [
                            {
                              "eq": [
                                "@key",
                                "hello"
                              ]
                            },
                            {
                              "eq": [
                                "@value",
                                "world"
                              ]
                            }
                          ]
                        }
                      ]
                    }
                    """)]
        [InlineData("""
                    {
                      "all": [
                        {
                          "ref": "HashtableLocal"
                        },
                        {
                          "ne": [
                            "@value",
                            "sun"
                          ]
                        }
                      ]
                    }
                    """)]
        [InlineData("""
                    {
                      "any": [
                        {
                          "filter": [
                            {
                              "ref": "HashtableLocal"
                            },
                            {
                              "eq": [
                                "@key",
                                "hello"
                              ]
                            }
                          ]
                        },
                        {
                          "eq": [
                            "@value",
                            "world"
                          ]
                        }
                      ]
                    }
                    """)]
        public void ProbeExpressionParser_NonGenericDictionaryPredicates_CanUseKeyAndValue(string json)
        {
            var hashtable = new Hashtable
            {
                { "hello", "world" },
                { "goodbye", "moon" },
            };

            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember("HashtableLocal", typeof(Hashtable), hashtable, ScopeMemberKind.Local));

            var compiled = ProbeExpressionParser<bool>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.True(result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_NonGenericDictionaryPredicates_CanUseExplicitKeyMember()
        {
            var hashtable = new Hashtable
            {
                { "hello", "world" },
                { "goodbye", "moon" },
            };

            var scopeMembers = CreateScopeMembers();
            scopeMembers.AddMember(new ScopeMember("HashtableLocal", typeof(Hashtable), hashtable, ScopeMemberKind.Local));

            const string json = """
                                {
                                  "any": [
                                    {
                                      "ref": "HashtableLocal"
                                    },
                                    {
                                      "eq": [
                                        {
                                          "getmember": [
                                            {
                                              "ref": "@it"
                                            },
                                            "Key"
                                          ]
                                        },
                                        "missing"
                                      ]
                                    }
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<bool>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.False(result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_NonGenericDictionaryDump_AllowsNullValues()
        {
            var scopeMembers = CreateScopeMembers();
            var holder = new HashtableHolder();
            scopeMembers.AddMember(new ScopeMember("HashtableHolderLocal", typeof(HashtableHolder), holder, ScopeMemberKind.Local));

            const string json = """
                                {
                                  "ref": "HashtableHolderLocal"
                                }
                                """;

            var compiled = ProbeExpressionParser<string>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Equal("{[hello, ], }", result);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_StaticPropertyOnCctorFreeType_IsUndefinedWithoutInvokingGetter()
        {
            var scopeMembers = CreateScopeMembers();
            scopeMembers.InvocationTarget = new ScopeMember("this", typeof(StaticExpressionWithoutCctor), new StaticExpressionWithoutCctor(), ScopeMemberKind.This);

            const string json = """
                                {
                                  "getmember": [
                                    {
                                      "ref": "this"
                                    },
                                    "StaticProperty"
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<string>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Equal(nameof(UndefinedValue), result);
            var error = Assert.Single(compiled.Errors);
            Assert.Contains("cannot be safely read", error.Message);
        }

        [Fact]
        public void ProbeExpressionParser_StaticMemberOnTypeWithCctor_IsUndefinedWithoutInitializingType()
        {
            _staticExpressionInitializedCount = 0;

            var scopeMembers = CreateScopeMembers();
            scopeMembers.InvocationTarget = new ScopeMember("this", typeof(StaticExpressionWithCctor), null, ScopeMemberKind.This);

            const string json = """
                                {
                                  "getmember": [
                                    {
                                      "ref": "this"
                                    },
                                    "StaticProperty"
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<object>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Same(UndefinedValue.Instance, result);
            Assert.Equal(0, _staticExpressionInitializedCount);
            var error = Assert.Single(compiled.Errors);
            Assert.Contains("could trigger the declaring type initializer", error.Message);
        }

        [Fact]
        public void ProbeExpressionParser_UndefinedValueStringDump_PreservesLegacyMarker()
        {
            var scopeMembers = CreateScopeMembers();

            const string json = """
                                {
                                  "ref": "missing"
                                }
                                """;

            var compiled = ProbeExpressionParser<string>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Equal(nameof(UndefinedValue), result);
            var error = Assert.Single(compiled.Errors);
            Assert.Contains("The property or field does not exist", error.Message);
        }

        [Fact]
        public void ProbeExpressionParser_StaticConstantOnTypeWithCctor_StillEvaluatesWithoutInitializingType()
        {
            _staticExpressionInitializedCount = 0;

            var scopeMembers = CreateScopeMembers();
            scopeMembers.InvocationTarget = new ScopeMember("this", typeof(StaticExpressionWithCctor), null, ScopeMemberKind.This);

            const string json = """
                                {
                                  "getmember": [
                                    {
                                      "ref": "this"
                                    },
                                    "ConstantField"
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<string>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Equal("constant-value", result);
            Assert.Equal(0, _staticExpressionInitializedCount);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        [Fact]
        public void ProbeExpressionParser_StaticEnumConstantOnTypeWithCctor_PreservesFieldType()
        {
            _staticExpressionInitializedCount = 0;

            var scopeMembers = CreateScopeMembers();
            scopeMembers.InvocationTarget = new ScopeMember("this", typeof(StaticExpressionWithCctor), null, ScopeMemberKind.This);

            const string json = """
                                {
                                  "getmember": [
                                    {
                                      "ref": "this"
                                    },
                                    "EnumConstantField"
                                  ]
                                }
                                """;

            var compiled = ProbeExpressionParser<StaticExpressionEnum>.ParseExpression(json, scopeMembers);
            var result = compiled.Delegate(
                scopeMembers.InvocationTarget,
                scopeMembers.Return,
                scopeMembers.Duration,
                scopeMembers.Exception,
                scopeMembers.Members);

            Assert.Equal(StaticExpressionEnum.One, result);
            Assert.Equal(0, _staticExpressionInitializedCount);
            Assert.True(compiled.Errors == null || compiled.Errors.Length == 0);
        }

        private static string CreateInstanceOfJson(string valueJson, string typeName)
        {
            return $$"""
                    {
                      "instanceof": [
                        {{valueJson}},
                        "{{typeName}}"
                      ]
                    }
                    """;
        }

        private static Assembly CreateDynamicAssembly(string assemblyName, params string[] typeNames)
        {
            var source = new StringBuilder();
            for (var i = 0; i < typeNames.Length; i++)
            {
                var typeName = typeNames[i];
                var lastDotIndex = typeName.LastIndexOf('.');
                source.Append("namespace ")
                      .Append(typeName.Substring(0, lastDotIndex))
                      .Append(" { public class ")
                      .Append(CreateDynamicTypeDeclaration(typeName.Substring(lastDotIndex + 1)))
                      .Append(" { } }");
            }

            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: [CSharpSyntaxTree.ParseText(source.ToString())],
                references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            result.Success.Should().BeTrue(string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));
            return Assembly.Load(stream.ToArray());
        }

        private static string CreateDynamicTypeDeclaration(string typeName)
        {
            var arityIndex = typeName.IndexOf('`');
            if (arityIndex < 0)
            {
                return typeName;
            }

            var arity = int.Parse(typeName.Substring(arityIndex + 1));
            var builder = new StringBuilder(typeName.Substring(0, arityIndex));
            builder.Append('<');
            for (var i = 0; i < arity; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append('T').Append(i);
            }

            builder.Append('>');
            return builder.ToString();
        }

        private async Task Test(string expressionTestFilePath)
        {
            // Arrange
            var evaluator = GetEvaluator(expressionTestFilePath);
            var settings = ConfigureVerifySettings(expressionTestFilePath);

            // Act
            var result = Evaluate(evaluator);

            // Assert
            var toVerify = GetStringToVerify(evaluator.Evaluator, result);
            await Verifier.Verify(toVerify, settings);
        }

        private (ProbeExpressionEvaluator Evaluator, MethodScopeMembers ScopeMembers) GetEvaluator(string expressionTestFilePath)
        {
            var jsonExpression = File.ReadAllText(expressionTestFilePath);
            var dsl = GetDslPart(jsonExpression);
            var json = GetJsonPart(jsonExpression);
            var scopeMembers = CreateScopeMembers();
            DebuggerExpression? condition = null;
            DebuggerExpression?[] templates;
            DebuggerExpression? metrics = null;
            KeyValuePair<DebuggerExpression?, KeyValuePair<string, DebuggerExpression?[]>[]>[] spanDecorations = null;
            var dirName = new DirectoryInfo(Path.GetDirectoryName(expressionTestFilePath)).Name;
            if (dirName == ConditionsFolder)
            {
                condition = new DebuggerExpression(dsl, json, null);
                templates = new DebuggerExpression?[] { new(DefaultDslTemplate, DefaultJsonTemplate, null) };
            }
            else if (dirName == TemplatesFolder)
            {
                templates = new DebuggerExpression?[] { new(null, null, "The result of the expression is: "), new(dsl, json, null) };
            }
            else if (dirName == MetricsFolder)
            {
                metrics = new DebuggerExpression(dsl, json, null);
                templates = new DebuggerExpression?[] { new(DefaultDslTemplate, DefaultJsonTemplate, null) };
            }
            else
            {
                throw new Exception($"{nameof(DebuggerExpressionLanguageTests)}.{nameof(GetEvaluator)}: Incorrect folder name");
            }

            return (new ProbeExpressionEvaluator(templates, condition, metrics, spanDecorations, captureExpressions: null), scopeMembers);
        }

        private VerifySettings ConfigureVerifySettings(string expressionTestFilePath)
        {
            var settings = new VerifySettings();
            settings.UseFileName($"{nameof(DebuggerExpressionLanguageTests)}.{Path.GetFileNameWithoutExtension(expressionTestFilePath)}");
            settings.DisableRequireUniquePrefix();
            settings.UseDirectory("ProbeExpressionsResources/Approvals");
            return settings;
        }

        private string GetDslPart(string json)
        {
            var reader = new JsonTextReader(new StringReader(json));

            while (reader.Read())
            {
                if (reader.TokenType != JsonToken.PropertyName)
                {
                    continue;
                }

                if (reader.Value?.ToString() == "dsl")
                {
                    reader.Read();
                    return reader.Value?.ToString();
                }
            }

            throw new InvalidOperationException("DSL part not found in the json file");
        }

        private string GetJsonPart(string json)
        {
            int startsFrom = json.IndexOf($",{Environment.NewLine}", StringComparison.Ordinal);
            return $"{Environment.NewLine}{{{json.Substring(startsFrom + 1)}";
        }

        private MethodScopeMembers CreateScopeMembers()
        {
            var scope = new MethodScopeMembers();
            scope.Set(new MethodScopeMembersParameters(10, 5));
            // Add locals
            scope.AddMember(new ScopeMember("IntLocal", TestObject.IntNumber.GetType(), TestObject.IntNumber, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("DoubleLocal", TestObject.DoubleNumber.GetType(), TestObject.DoubleNumber, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("StringLocal", TestObject.String.GetType(), TestObject.String, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("CollectionLocal", TestObject.Collection.GetType(), TestObject.Collection, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("CollectionIntLocal", TestObject.CollectionInt.GetType(), TestObject.CollectionInt, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("HashIntLocal", TestObject.HashInt.GetType(), TestObject.HashInt, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("ArrayLocal", TestObject.Array.GetType(), TestObject.Array, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("CustomArrayLocal", TestObject.CustomArray.GetType(), TestObject.CustomArray, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("DictionaryLocal", TestObject.Dictionary.GetType(), TestObject.Dictionary, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("NestedObjectLocal", TestObject.Nested.GetType(), TestObject.Nested, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("NullLocal", TestObject.Nested.GetType(), TestObject.Null, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("BooleanValue", TestObject.BooleanValue.GetType(), TestObject.BooleanValue, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("Char", TestObject.Char.GetType(), TestObject.Char, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("AnotherChar", TestObject.AnotherChar.GetType(), TestObject.AnotherChar, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("NullableNotNullValueLocal", typeof(Guid?), TestObject.NullableNotNullValue, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("NullableNullValueLocal", typeof(Guid?), TestObject.NullableNullValue, ScopeMemberKind.Local));

            // Add arguments
            scope.AddMember(new ScopeMember("IntArg", TestObject.IntNumber.GetType(), TestObject.IntNumber, ScopeMemberKind.Argument));
            scope.AddMember(new ScopeMember("DoubleArg", TestObject.DoubleNumber.GetType(), TestObject.DoubleNumber, ScopeMemberKind.Argument));
            scope.AddMember(new ScopeMember("StringArg", TestObject.String.GetType(), TestObject.String, ScopeMemberKind.Argument));
            scope.AddMember(new ScopeMember("CollectionArg", TestObject.Collection.GetType(), TestObject.Collection, ScopeMemberKind.Argument));
            scope.AddMember(new ScopeMember("NestedObjectArg", TestObject.Nested.GetType(), TestObject.Nested, ScopeMemberKind.Argument));

            // Add "this" member
            scope.InvocationTarget = new ScopeMember("this", TestObject.GetType(), TestObject, ScopeMemberKind.This);

            // Add "return" member
            scope.Return = new ScopeMember("Dummy Return", typeof(string), "I'm a return value", ScopeMemberKind.Return);

            // Add "duration" member
            scope.Duration = new ScopeMember("@duration", typeof(TimeSpan), TimeSpan.FromMilliseconds(20), ScopeMemberKind.Duration);

            // Add "exception" member
            scope.Exception = new InvalidCastException("Can not cast X to Y");

            return scope;
        }

        private (string Template, bool? Condition, double? Metric, List<EvaluationError> Errors) Evaluate((ProbeExpressionEvaluator Evaluator, MethodScopeMembers ScopeMembers) evaluator)
        {
            var result = evaluator.Evaluator.Evaluate(evaluator.ScopeMembers);
            return (result.Template, result.Condition, result.Metric, result.Errors);
        }

        private string GetStringToVerify(ProbeExpressionEvaluator evaluator, (string Template, bool? Condition, double? Metric, List<EvaluationError> Errors) evaluationResult)
        {
            var builder = new StringBuilder();
            if (evaluationResult.Condition.HasValue)
            {
                builder.AppendLine("Condition:");
                builder.AppendLine($"Json:{evaluator.Condition.Value.Json}");
                builder.AppendLine($"Expression: {SanitizeExpression(evaluator.CompiledCondition.Value.ParsedExpression.ToReadableString())}");
                builder.AppendLine($"Result: {evaluationResult.Condition}");
            }

            if (evaluator.Templates.Any(t => t?.Dsl != DefaultDslTemplate))
            {
                builder.AppendLine("Template:");
                builder.AppendLine($"Segments: {string.Join(Environment.NewLine, evaluator.Templates.Select(t => t?.Json))}");
                builder.AppendLine($"Expressions: {string.Join(Environment.NewLine, evaluator.CompiledTemplates.Select(t => t.ParsedExpression.ToReadableString()))}");
                builder.AppendLine($"Result: {SanitizeEvaluationResult(evaluationResult.Template)}");
            }

            if (evaluationResult.Metric.HasValue)
            {
                builder.AppendLine("Metric:");
                builder.AppendLine($"Json:{evaluator.Metric.Value.Json}");
                builder.AppendLine($"Expression: {evaluator.CompiledMetric.Value.ParsedExpression.ToReadableString()}");
                builder.AppendLine($"Result: {evaluationResult.Metric}");
            }

            if (evaluationResult.Errors is { Count: > 0 })
            {
                builder.AppendLine("Errors:");
                builder.AppendLine($"{string.Join(Environment.NewLine, evaluationResult.Errors.Select(SanitizeEvaluationErrorStrings))}");
            }

            return builder.ToString();
        }

        private string SanitizeExpression(string expression)
        {
            string pattern = @",(?=\d*d\b)";
            return Regex.Replace(expression, pattern, ".");
        }

        private string SanitizeEvaluationResult(string template)
        {
            // remove corlib assembly name
            template = template.Replace("mscorlib, ", string.Empty).Replace("System.Private.CoreLib, ", string.Empty);
            template = template.Replace("Empty=00000000-0000-0000-0000-000000000000, ", string.Empty);
            template = template.Replace("_a=0, _b=0, _c=0, _d=0, _e=0, ...", "_a=0, _b=0, _c=0, _d=0, ...");

            // remove assembly PublicKeyToken
            var tokenStartString = ", PublicKeyToken=";
            var tokenIndex = template.IndexOf(tokenStartString);
            if (tokenIndex >= 0)
            {
                const int guidLength = 16;
                template = template.Substring(0, tokenIndex) + template.Substring(tokenIndex + tokenStartString.Length + guidLength, template.Length - (tokenIndex + tokenStartString.Length + guidLength));
            }

            // remove assembly version
            const string versionExample = "Version=0.0.0.0, ";
            var versionIndex = template.IndexOf("Version=");
            if (versionIndex >= 0)
            {
                template = template.Substring(0, versionIndex) + template.Substring(versionIndex + versionExample.Length, template.Length - (versionIndex + versionExample.Length));
            }

            template = template.Replace(",  Culture=", ", Culture=");

            return template;
        }

        private EvaluationError SanitizeEvaluationErrorStrings(EvaluationError error)
        {
            // The exception.Message returns different string depend on runtime version

            var parameterNameIndex = error.Message.LastIndexOf($"{Environment.NewLine}Parameter name: ", StringComparison.CurrentCultureIgnoreCase);
            if (parameterNameIndex > 0 && parameterNameIndex + 1 < error.Message.Length)
            {
                error.Message = error.Message.Substring(0, parameterNameIndex);
            }
            else
            {
                parameterNameIndex = error.Message.LastIndexOf(" (Parameter ", StringComparison.CurrentCultureIgnoreCase);
                if (parameterNameIndex > 0 && parameterNameIndex + 1 < error.Message.Length)
                {
                    error.Message = error.Message.Substring(0, parameterNameIndex);
                }
            }

            // The expression.ToString returns different string depend on runtime version
            error.Expression = error.Expression.Replace("Convert(CollectionLocal.get_Item(100), String))", "Convert(CollectionLocal.get_Item(100)))");
            return error;
        }

        internal struct TestStruct
        {
            public int IntNumber;

            public List<string> Collection;

            public List<int> CollectionInt;

            public HashSet<int> HashInt;

            public string[] Array;

            public NestedObject[] CustomArray;

            public Dictionary<string, string> Dictionary;

            public double DoubleNumber;

            public string String;

            public char Char;

            public char AnotherChar;

            public NestedObject Nested;

            public ChildNestedObject ChildNested;

            public NestedObject ParentAsChildNested;

            public NestedObject Null;

            public bool BooleanValue;

            public Guid? NullableNullValue;

            public Guid? NullableNotNullValue;

            public string EmptyString { get; set; }

            internal class NestedObject
            {
#pragma warning disable SA1401
#pragma warning disable SA1306
                // ReSharper disable once UnusedMember.Global
                protected static string ParentProtectedStaticMember = "Hello from parent protected static member";
#pragma warning restore SA1401 // Field is assigned but its value is never used
#pragma warning restore SA1306

#pragma warning disable CS0414 // Field is assigned but its value is never used
                private string _parentPrivateMember = "Hello from parent private member";
#pragma warning restore CS0414 // Field is assigned but its value is never used

                private NestedObject _circleRef;

                private TimeSpan _timeSpan = new TimeSpan();

                private Dictionary<string, int> _dictionary = new Dictionary<string, int>() { { "one", 1 }, { "two", 2 }, { "three", 3 }, { "four", 4 } };

                private IEnumerable<int> _ienumerable = Enumerable.Range(0, 4);

                private IReadOnlyList<int> _readonlyList = new ArraySegment<int>(new int[] { 1, 2, 3, 4 });

                private string _string = "I'm a string field";

                private List<List<int>> _listOfLists = new List<List<int>>()
                {
                    new List<int>()
                    {
                        1,
                        2,
                        3,
                        4,
                    },
                    new List<int>()
                    {
                        1,
                        2,
                        3,
                        4,
                    },
                    new List<int>()
                    {
                        1,
                        2,
                        3,
                        4,
                    },
                    new List<int>()
                    {
                        1,
                        2,
                        3,
                        4,
                    }
                };

                public string NestedString { get; set; }

                public NestedObject Nested { get; set; }

                public void CreateCircleRef()
                {
                    _circleRef = new NestedObject();
                    _circleRef._circleRef = new NestedObject();
                }

                public override string ToString()
                {
                    return _string + _timeSpan.ToString() + _dictionary.ToString() + _ienumerable.ToString() + _listOfLists.ToString() + _readonlyList.ToString();
                }
            }

            internal class ChildNestedObject : NestedObject
            {
#pragma warning disable SA1401
                // ReSharper disable once UnusedMember.Global
                public static string ChildPublicStaticMember = "Hello from child public static static member";
#pragma warning restore SA1401

#pragma warning disable CS0414 // Field is assigned but its value is never used
                private string _childPrivateMember = "Hello from child private member";
#pragma warning restore CS0414 // Field is assigned but its value is never used
            }
        }

        internal class GenericReferenceTypeTarget<T>
            where T : class
        {
            public IEnumerable<T> Collection { get; set; }
        }

        internal sealed class CountingEnumerable<T> : IEnumerable<T>
        {
            private readonly List<T> _items;

            internal CountingEnumerable(IEnumerable<T> items)
            {
                _items = new List<T>(items);
            }

            internal int VisitedItems { get; private set; }

            public IEnumerator<T> GetEnumerator()
            {
                foreach (var item in _items)
                {
                    VisitedItems++;
                    yield return item;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        internal class StaticExpressionWithoutCctor
        {
            public static string StaticProperty => "safe-property";
        }

        internal class StaticExpressionWithCctor
        {
            public const string ConstantField = "constant-value";

            public const StaticExpressionEnum EnumConstantField = StaticExpressionEnum.One;

            static StaticExpressionWithCctor()
            {
                _staticExpressionInitializedCount++;
            }

            public static string StaticProperty { get; } = "unsafe-property";
        }

        internal class GenericValueTypeTarget<T>
            where T : struct
        {
        }

        internal class RecursiveGenericConstraintTarget<T>
            where T : IComparable<T>
        {
        }

        internal class SameParameterNameOverloads
        {
            public static string Match<T>(String value, T genericValue)
            {
                return "custom";
            }

            public static string Match<T>(string value, T genericValue)
            {
                return "system";
            }
        }

        internal sealed class String
        {
        }

        private class HashtableHolder
        {
            private readonly Hashtable dictionary = new()
            {
                { "hello", null },
            };
        }

        private sealed class ThrowingGetTypeAssembly : Assembly
        {
            private readonly string _fullName;

            public ThrowingGetTypeAssembly(string name)
            {
                _fullName = $"{name}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
            }

            public override string FullName => _fullName;

            public override Type GetType(string name, bool throwOnError, bool ignoreCase)
            {
                throw new TypeLoadException("Test assembly cannot inspect types.");
            }
        }

        private sealed class ThrowsOnToStringKey
        {
            public override string ToString()
            {
                throw new InvalidOperationException("Dictionary key ToString should not be called.");
            }
        }

        private sealed class ThrowsOnEqualsValue
        {
            public override bool Equals(object obj)
            {
                throw new InvalidOperationException("Dictionary value Equals should not be called.");
            }

            public override int GetHashCode()
            {
                return 0;
            }
        }

        private class SensitiveValueHolder
        {
            public string Data { get; set; }
        }

        private class AutoPropertyTarget
        {
            public string Value { get; set; }
        }

        private class BaseAutoPropertyTarget
        {
            public string BaseValue { get; set; }
        }

        private class DerivedAutoPropertyTarget : BaseAutoPropertyTarget
        {
        }

        private class SideEffectingPropertyTarget
        {
            private static int _getterCalls;

            public static int GetterCalls => _getterCalls;

            public string UnsafeValue
            {
                get
                {
                    _getterCalls++;
                    return "unsafe";
                }
            }

            public static void Reset()
            {
                _getterCalls = 0;
            }
        }

        private class SideEffectingValueHolder
        {
            private static int _getterCalls;

            public SideEffectingValueHolder(string data)
            {
                Data = data;
            }

            public static int GetterCalls => _getterCalls;

            public string Data { get; }

            public string UnsafeData
            {
                get
                {
                    _getterCalls++;
                    return Data;
                }
            }

            public static void Reset()
            {
                _getterCalls = 0;
            }
        }
    }
}
