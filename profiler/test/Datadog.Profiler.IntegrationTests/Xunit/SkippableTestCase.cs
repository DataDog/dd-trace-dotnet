// <copyright file="SkippableTestCase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Xunit
{
    [Serializable]
    public class SkippableTestCase : TestMethodTestCase, IXunitTestCase
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer", error: true)]
        public SkippableTestCase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SkippableTestCase"/> class.
        /// </summary>
        /// <param name="testMethod">The test method this test case belongs to.</param>
        /// <param name="testMethodArguments">The arguments for the test method.</param>
        public SkippableTestCase(string skipReason, ITestMethod testMethod, object[] testMethodArguments = null)
            : base(TestMethodDisplay.ClassAndMethod, TestMethodDisplayOptions.None, testMethod, testMethodArguments)
        {
            SkipReason = skipReason;
        }

        public int Timeout => throw new NotImplementedException();

        public override void Serialize(IXunitSerializationInfo data)
        {
            base.Serialize(data);

            data.AddValue("SkipReason", SkipReason);
        }

        public override void Deserialize(IXunitSerializationInfo data)
        {
            base.Deserialize(data);

            SkipReason = data.GetValue<string>("SkipReason");
        }

        public Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            return new XunitTestCaseRunner(this, DisplayName, SkipReason, constructorArguments, TestMethodArguments, messageBus, aggregator, cancellationTokenSource).RunAsync();
        }
    }
}
