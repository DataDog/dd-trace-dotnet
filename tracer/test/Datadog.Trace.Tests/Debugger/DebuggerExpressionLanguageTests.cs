// <copyright file="DebuggerExpressionLanguageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AgileObjects.ReadableExpressions;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Vendors.Newtonsoft.Json;
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

        public DebuggerExpressionLanguageTests()
        {
            TestObject = new TestStruct
            {
                Collection = new List<string> { "hello", "1st Item", "2nd item", "3rd item" },
                Dictionary = new Dictionary<string, string> { { "hello", "world" } },
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

        internal TestStruct TestObject { get; set; }

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
            DebuggerExpression[] templates;
            DebuggerExpression? metrics = null;
            KeyValuePair<DebuggerExpression?, KeyValuePair<string, DebuggerExpression[]>[]>[] spanDecorations = null;
            var dirName = new DirectoryInfo(Path.GetDirectoryName(expressionTestFilePath)).Name;
            if (dirName == ConditionsFolder)
            {
                condition = new DebuggerExpression(dsl, json, null);
                templates = new DebuggerExpression[] { new(DefaultDslTemplate, DefaultJsonTemplate, null) };
            }
            else if (dirName == TemplatesFolder)
            {
                templates = new DebuggerExpression[] { new(null, null, "The result of the expression is: "), new(dsl, json, null) };
            }
            else if (dirName == MetricsFolder)
            {
                metrics = new DebuggerExpression(dsl, json, null);
                templates = new DebuggerExpression[] { new(DefaultDslTemplate, DefaultJsonTemplate, null) };
            }
            else
            {
                throw new Exception($"{nameof(DebuggerExpressionLanguageTests)}.{nameof(GetEvaluator)}: Incorrect folder name");
            }

            return (new ProbeExpressionEvaluator(templates, condition, metrics, spanDecorations), scopeMembers);
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
            var scope = new MethodScopeMembers(10, 5);

            // Add locals
            scope.AddMember(new ScopeMember("IntLocal", TestObject.IntNumber.GetType(), TestObject.IntNumber, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("DoubleLocal", TestObject.DoubleNumber.GetType(), TestObject.DoubleNumber, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("StringLocal", TestObject.String.GetType(), TestObject.String, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("CollectionLocal", TestObject.Collection.GetType(), TestObject.Collection, ScopeMemberKind.Local));
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

            if (evaluator.Templates.Any(t => t.Dsl != DefaultDslTemplate))
            {
                builder.AppendLine("Template:");
                builder.AppendLine($"Segments: {string.Join(Environment.NewLine, evaluator.Templates.Select(t => t.Json))}");
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
    }
}
