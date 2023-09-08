// <copyright file="ProfilerTestCaseRunner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Xunit
{
    internal class ProfilerTestCaseRunner : XunitTestCaseRunner
    {
        public ProfilerTestCaseRunner(IXunitTestCase testCase, string displayName, string skipReason, object[] constructorArguments, object[] testMethodArguments, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            : base(testCase, displayName, skipReason, constructorArguments, testMethodArguments, messageBus, aggregator, cancellationTokenSource)
        {
        }

        protected override List<BeforeAfterTestAttribute> GetBeforeAfterTestAttributes()
        {
            var baseList = base.GetBeforeAfterTestAttributes();
            baseList.Add(new ProfilerBeforeAfterTestAttribute());
            return baseList;
        }
    }
}
