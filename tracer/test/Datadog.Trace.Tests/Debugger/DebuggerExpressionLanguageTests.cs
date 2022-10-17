// <copyright file="DebuggerExpressionLanguageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgileObjects.ReadableExpressions;
using Datadog.Trace.Debugger.Conditions;
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

        public static IEnumerable<object[]> DSLExamples()
        {
            var path = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName, "Debugger", "Resources");
            return Directory.EnumerateFiles(path).Where(f => !Path.GetFileName(f).StartsWith("Skip")).Select(file => new object[] { file });
        }

        [Theory]
        [MemberData(nameof(DSLExamples))]
        public async Task TestCondition(string conditionTestFilePath)
        {
            var conditionTest = File.ReadAllText(conditionTestFilePath);
            var probeCondition = new ProbeCondition(" ", MethodPhase.End, conditionTest);
            PopulateMembers(ref probeCondition);
            var condition = ProbeConditionExpressionParser.ToCondition(conditionTest, probeCondition.InvocationTarget, probeCondition.ScopeMembers);
            var result = probeCondition.Evaluate();
            var toVerify = $"DSL: {condition.DSL}{Environment.NewLine}Expression: {condition.Expression.ToReadableString()}{Environment.NewLine}Result: {result}";

            var settings = new VerifySettings();
            settings.UseFileName($"{nameof(DebuggerExpressionLanguageTests)}.{Path.GetFileNameWithoutExtension(conditionTestFilePath)}");
            settings.DisableRequireUniquePrefix();
            VerifierSettings.DerivePathInfo(
                (sourceFile, _, _, _) => new PathInfo(directory: Path.Combine(sourceFile, "..", "conditions")));
            await Verifier.Verify(toVerify, settings);
        }

        private void PopulateMembers(ref ProbeCondition probeCondition)
        {
            // Add locals
            probeCondition.AddLocal("IntLocal", Test.IntNumber.GetType(), Test.IntNumber);
            probeCondition.AddLocal("DoubleLocal", Test.DoubleNumber.GetType(), Test.DoubleNumber);
            probeCondition.AddLocal("StringLocal", Test.String.GetType(), Test.String);
            probeCondition.AddLocal("CollectionLocal", Test.Collection.GetType(), Test.Collection);
            probeCondition.AddLocal("NestedObjectLocal", Test.Nested.GetType(), Test.Nested);

            // Add arguments
            probeCondition.AddArgument("IntArg", Test.IntNumber.GetType(), Test.IntNumber);
            probeCondition.AddArgument("DoubleArg", Test.DoubleNumber.GetType(), Test.DoubleNumber);
            probeCondition.AddArgument("StringArg", Test.String.GetType(), Test.String);
            probeCondition.AddArgument("CollectionArg", Test.Collection.GetType(), Test.Collection);
            probeCondition.AddArgument("NestedObjectArg", Test.Nested.GetType(), Test.Nested);

            // Add "this" member
            probeCondition.SetInvocationTarget("this", Test.GetType(), Test);
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
