// <copyright file="DebuggerExpressionLanguageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AgileObjects.ReadableExpressions;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    [UsesVerify]
    public class DebuggerExpressionLanguageTests
    {
        public DebuggerExpressionLanguageTests()
        {
            Test = new TestStruct
            {
                Collection = new List<string> { "hello", "1st Item", "2nd item", "3rd item" },
                IntNumber = 42,
                DoubleNumber = 3.14159,
                String = "Hello world!",
                Nested = new TestStruct.NestedObject() { NestedString = "Hello from nested object" }
            };
        }

        internal TestStruct Test { get; set; }

        public static IEnumerable<object[]> ExpressionsResources()
        {
            var sourceFilePath = GetSourceFilePath();
            var path = Path.Combine(sourceFilePath, "..", "ProbeExpressionsResources");
            return Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly).Select(file => new object[] { file });
        }

        public static string GetSourceFilePath([CallerFilePath] string sourceFilePath = null)
        {
            return sourceFilePath ?? throw new InvalidOperationException("Can't obtain source file path");
        }

        [Theory]
        [MemberData(nameof(ExpressionsResources))]
        public async Task TestExpression(string expressionTestFilePath)
        {
            // Arrange
            var evaluator = GetEvaluator(expressionTestFilePath);
            var settings = ConfigureVerifySettings(expressionTestFilePath);

            // Act
            var compiledExpression = ProbeExpressionParser<bool>.ParseExpression(evaluator.Condition.Value.Json, evaluator.ScopeMembers.InvocationTarget, evaluator.ScopeMembers.Members);
            var result = Evaluate(evaluator);

            // Assert
            Assert.True(result.Succeeded);
            var toVerify = $"Json: {compiledExpression.RawExpression}{Environment.NewLine}Expression: {compiledExpression.ParsedExpression.ToReadableString()}{Environment.NewLine}Result: {result.ConditionResult}";
            await Verifier.Verify(toVerify, settings);
        }

        private (bool Succeeded, bool ConditionResult) Evaluate(ProbeExpressionEvaluator evaluator)
        {
            var result = evaluator.Evaluate();
            return (result.Succeeded, result.Condition.Value);
        }

        private VerifySettings ConfigureVerifySettings(string expressionTestFilePath)
        {
            var settings = new VerifySettings();
            settings.UseFileName($"{nameof(DebuggerExpressionLanguageTests)}.{Path.GetFileNameWithoutExtension(expressionTestFilePath)}");
            settings.DisableRequireUniquePrefix();
            VerifierSettings.DerivePathInfo(
                (sourceFile, _, _, _) => new PathInfo(directory: Path.Combine(sourceFile, "..", "ProbeExpressionsResources", "Approvals")));
            return settings;
        }

        private ProbeExpressionEvaluator GetEvaluator(string expressionTestFilePath)
        {
            var jsonExpression = File.ReadAllText(expressionTestFilePath);
            var dsl = GetDsl(jsonExpression);
            var scopeMembers = CreateScopeMembers();
            var evaluator = new ProbeExpressionEvaluator(null, new DebuggerExpression(dsl, jsonExpression, null), null, scopeMembers);
            return evaluator;
        }

        private string GetDsl(string expressionJson)
        {
            var reader = new JsonTextReader(new StringReader(expressionJson));

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

        private MethodScopeMembers CreateScopeMembers()
        {
            var scope = new MethodScopeMembers(5, 5);

            // Add locals
            scope.AddMember(new ScopeMember("IntLocal", Test.IntNumber.GetType(), Test.IntNumber, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("DoubleLocal", Test.DoubleNumber.GetType(), Test.DoubleNumber, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("StringLocal", Test.String.GetType(), Test.String, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("CollectionLocal", Test.Collection.GetType(), Test.Collection, ScopeMemberKind.Local));
            scope.AddMember(new ScopeMember("NestedObjectLocal", Test.Nested.GetType(), Test.Nested, ScopeMemberKind.Local));

            // Add arguments
            scope.AddMember(new ScopeMember("IntArg", Test.IntNumber.GetType(), Test.IntNumber, ScopeMemberKind.Argument));
            scope.AddMember(new ScopeMember("DoubleArg", Test.DoubleNumber.GetType(), Test.DoubleNumber, ScopeMemberKind.Argument));
            scope.AddMember(new ScopeMember("StringArg", Test.String.GetType(), Test.String, ScopeMemberKind.Argument));
            scope.AddMember(new ScopeMember("CollectionArg", Test.Collection.GetType(), Test.Collection, ScopeMemberKind.Argument));
            scope.AddMember(new ScopeMember("NestedObjectArg", Test.Nested.GetType(), Test.Nested, ScopeMemberKind.Argument));

            // Add "this" member
            scope.InvocationTarget = new ScopeMember("this", Test.GetType(), Test, ScopeMemberKind.This);

            return scope;
        }

        internal struct TestStruct
        {
            public int IntNumber;

            public List<string> Collection;

            public double DoubleNumber;

            public string String;

            public NestedObject Nested;

            internal struct NestedObject
            {
                public string NestedString;
            }
        }
    }
}
