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
using Datadog.Trace.Debugger.Conditions;
using Datadog.Trace.Debugger.Configurations.Models;
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
            Test = new TestStruct()
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
            var evaluator = GetJsonExpression(expressionTestFilePath);
            var settings = ConfigureVerifySettings(expressionTestFilePath);

            // Act
            var compiledExpression = ProbeExpressionParser.ParseExpression<bool>(evaluator.DebuggerExpressions.First().Json, evaluator.MethodScopeMembers.InvocationTarget, evaluator.MethodScopeMembers.Members);
            var result = evaluator.Evaluate();

            // Assert
            Assert.True(result.Succeeded);
            var toVerify = $"Expression: {compiledExpression.ParsedExpression.ToReadableString()}{Environment.NewLine}Result: {result.Condition}";
            await Verifier.Verify(toVerify, settings);
        }

        private static VerifySettings ConfigureVerifySettings(string expressionTestFilePath)
        {
            var settings = new VerifySettings();
            settings.UseFileName($"{nameof(DebuggerExpressionLanguageTests)}.{Path.GetFileNameWithoutExtension(expressionTestFilePath)}");
            settings.DisableRequireUniquePrefix();
            VerifierSettings.DerivePathInfo(
                (sourceFile, _, _, _) => new PathInfo(directory: Path.Combine(sourceFile, "..", "ProbeExpressionsResources", "Approvals")));
            return settings;
        }

        private ProbeConditionEvaluator GetJsonExpression(string expressionTestFilePath)
        {
            var jsonExpression = File.ReadAllText(expressionTestFilePath);
            var dsl = GetDsl(jsonExpression);
            var probeExpression = new ProbeConditionEvaluator(" ", EvaluateAt.Exit, new DebuggerExpression[] { new(dsl, jsonExpression) });
            PopulateMembers(probeExpression);
            return probeExpression;
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

        private void PopulateMembers(ProbeExpressionEvaluatorBase probeExpressionEvaluator)
        {
            probeExpressionEvaluator.CreateMethodScopeMembers(5, 5);
            // Add locals
            probeExpressionEvaluator.AddMember("IntLocal", Test.IntNumber.GetType(), Test.IntNumber, ScopeMemberKind.Local);
            probeExpressionEvaluator.AddMember("DoubleLocal", Test.DoubleNumber.GetType(), Test.DoubleNumber, ScopeMemberKind.Local);
            probeExpressionEvaluator.AddMember("StringLocal", Test.String.GetType(), Test.String, ScopeMemberKind.Local);
            probeExpressionEvaluator.AddMember("CollectionLocal", Test.Collection.GetType(), Test.Collection, ScopeMemberKind.Local);
            probeExpressionEvaluator.AddMember("NestedObjectLocal", Test.Nested.GetType(), Test.Nested, ScopeMemberKind.Local);

            // Add arguments
            probeExpressionEvaluator.AddMember("IntArg", Test.IntNumber.GetType(), Test.IntNumber, ScopeMemberKind.Argument);
            probeExpressionEvaluator.AddMember("DoubleArg", Test.DoubleNumber.GetType(), Test.DoubleNumber, ScopeMemberKind.Argument);
            probeExpressionEvaluator.AddMember("StringArg", Test.String.GetType(), Test.String, ScopeMemberKind.Argument);
            probeExpressionEvaluator.AddMember("CollectionArg", Test.Collection.GetType(), Test.Collection, ScopeMemberKind.Argument);
            probeExpressionEvaluator.AddMember("NestedObjectArg", Test.Nested.GetType(), Test.Nested, ScopeMemberKind.Argument);

            // Add "this" member
            probeExpressionEvaluator.AddInvocationTarget("this", Test.GetType(), Test);
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
