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

        public DebuggerExpressionLanguageTests()
        {
            Test = new TestStruct
            {
                Collection = new List<string> { "hello", "1st Item", "2nd item", "3rd item" },
                Dictionary = new Dictionary<string, string> { { "hello", "world" } },
                IntNumber = 42,
                DoubleNumber = 3.14159,
                String = "Hello world!",
                Null = null,
                Nested = new TestStruct.NestedObject { NestedString = "Hello from nested object", Nested = new TestStruct.NestedObject { NestedString = "Hello from another nested object" } }
            };
        }

        internal TestStruct Test { get; set; }

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

        public static string GetSourceFilePath([CallerFilePath] string sourceFilePath = null)
        {
            return sourceFilePath ?? throw new InvalidOperationException("Can't obtain source file path");
        }

        [SkippableTheory]
        [MemberData(nameof(TemplatesResources))]
        public async Task TestTemplates(string expressionTestFilePath)
        {
            if (expressionTestFilePath.EndsWith("DictionaryKeyNotExist.json"))
            {
                throw new SkipException("Skip because this test has an issue of not raising a KeyNotFoundException");
            }

            // Arrange
            var evaluator = GetEvaluator(expressionTestFilePath);
            var settings = ConfigureVerifySettings(expressionTestFilePath);

            // Act
            var result = Evaluate(evaluator);

            // Assert
            Assert.NotNull(result.Template);
            Assert.True(evaluator.Evaluator.CompiledTemplates.Length > 0);
            var toVerify = GetStringToVerify(evaluator.Evaluator, result);
            await Verifier.Verify(toVerify, settings);
        }

        [Theory]
        [MemberData(nameof(ConditionsResources))]
        public async Task TestConditions(string expressionTestFilePath)
        {
            // Arrange
            var evaluator = GetEvaluator(expressionTestFilePath);
            var settings = ConfigureVerifySettings(expressionTestFilePath);

            // Act
            var result = Evaluate(evaluator);

            // Assert
            Assert.NotNull(result.Template);
            Assert.True(result.Condition.HasValue);
            Assert.True(evaluator.Evaluator.CompiledTemplates.Length > 0);
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
            if (new DirectoryInfo(Path.GetDirectoryName(expressionTestFilePath)).Name == ConditionsFolder)
            {
                condition = new DebuggerExpression(dsl, json, null);
                templates = new DebuggerExpression[] { new(DefaultDslTemplate, DefaultJsonTemplate, null) };
            }
            else
            {
                templates = new DebuggerExpression[] { new(null, null, "The result of the expression is: "), new(dsl, json, null) };
            }

            return (new ProbeExpressionEvaluator(templates, condition, null), scopeMembers);
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
            var scope = new MethodScopeMembers(5, 5);

            // Add locals
            scope.AddMember(new ScopeMember("IntLocal", Test.IntNumber.GetType(), Test.IntNumber, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("DoubleLocal", Test.DoubleNumber.GetType(), Test.DoubleNumber, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("StringLocal", Test.String.GetType(), Test.String, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("CollectionLocal", Test.Collection.GetType(), Test.Collection, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("DictionaryLocal", Test.Dictionary.GetType(), Test.Dictionary, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("NestedObjectLocal", Test.Nested.GetType(), Test.Nested, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("NullLocal", Test.Nested.GetType(), Test.Null, ScopeMemberKind.Local));

            // Add arguments
            scope.AddMember(new ScopeMember("IntArg", Test.IntNumber.GetType(), Test.IntNumber, ScopeMemberKind.Argument));
            scope.AddMember(new ScopeMember("DoubleArg", Test.DoubleNumber.GetType(), Test.DoubleNumber, ScopeMemberKind.Argument));
            scope.AddMember(new ScopeMember("StringArg", Test.String.GetType(), Test.String, ScopeMemberKind.Argument));
            scope.AddMember(new ScopeMember("CollectionArg", Test.Collection.GetType(), Test.Collection, ScopeMemberKind.Argument));
            scope.AddMember(new ScopeMember("NestedObjectArg", Test.Nested.GetType(), Test.Nested, ScopeMemberKind.Argument));

            // Add "this" member
            scope.InvocationTarget = new ScopeMember("this", Test.GetType(), Test, ScopeMemberKind.This);

            // Add "return" member
            scope.Return = new ScopeMember("Dummy Return", typeof(string), "I'm a return value", ScopeMemberKind.Return);

            // Add "duration" member
            scope.Duration = new ScopeMember("@duration", typeof(TimeSpan), TimeSpan.FromMilliseconds(20), ScopeMemberKind.Duration);

            // Add "exception" member
            scope.Exception = new InvalidCastException("Can not cast X to Y");

            return scope;
        }

        private (string Template, bool? Condition, List<EvaluationError> Errors) Evaluate((ProbeExpressionEvaluator Evaluator, MethodScopeMembers ScopeMembers) evaluator)
        {
            var result = evaluator.Evaluator.Evaluate(evaluator.ScopeMembers);
            return (result.Template, result.Condition, result.Errors);
        }

        private string GetStringToVerify(ProbeExpressionEvaluator evaluator, (string Template, bool? Condition, List<EvaluationError> Errors) evaluationResult)
        {
            var builder = new StringBuilder();
            if (evaluationResult.Condition.HasValue)
            {
                builder.AppendLine("Condition:");
                builder.AppendLine($"Json:{evaluator.Condition.Value.Json}");
                builder.AppendLine($"Expression: {evaluator.CompiledCondition.Value.ParsedExpression.ToReadableString()}");
                builder.AppendLine($"Result: {evaluationResult.Condition}");
            }

            if (evaluator.Templates.Any(t => t.Dsl != DefaultDslTemplate))
            {
                builder.AppendLine("Template:");
                builder.AppendLine($"Segments: {string.Join(Environment.NewLine, evaluator.Templates.Select(t => t.Json))}");
                builder.AppendLine($"Expressions: {string.Join(Environment.NewLine, evaluator.CompiledTemplates.Select(t => t.ParsedExpression.ToReadableString()))}");
                builder.AppendLine($"Result: {evaluationResult.Template}");
            }

            if (evaluationResult.Errors is { Count: > 0 })
            {
                builder.AppendLine("Errors:");
                builder.AppendLine($"{string.Join(Environment.NewLine, evaluationResult.Errors.Select(SanitizeEvaluationErrorStrings))}");
            }

            return builder.ToString();
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

            return SanitizeExpressionMessage(error);
        }

        private EvaluationError SanitizeExpressionMessage(EvaluationError error)
        {
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

            public NestedObject Nested;

            public NestedObject Null;

            internal class NestedObject
            {
                public string NestedString { get; set; }

                public NestedObject Nested { get; set; }
            }
        }
    }
}
