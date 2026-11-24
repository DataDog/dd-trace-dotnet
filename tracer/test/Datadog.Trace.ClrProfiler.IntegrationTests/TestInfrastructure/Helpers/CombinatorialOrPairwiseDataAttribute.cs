// <copyright file="CombinatorialOrPairwiseDataAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    public class CombinatorialOrPairwiseDataAttribute : DataAttribute
    {
        public static bool UseFullTestConfiguration()
        {
            // USE_FULL_TEST_CONFIG is set in Target RunIntegrationTests when RequiresThoroughTesting() is true
            var fullTest = Environment.GetEnvironmentVariable("USE_FULL_TEST_CONFIG") ?? string.Empty;

            if (string.IsNullOrEmpty(fullTest))
            {
                // Default to true if the environment variable is not set - locally just run everything
                return true;
            }

            // otherwise, only run the full suite if we are on master / release
            return string.Equals(fullTest, "True", StringComparison.OrdinalIgnoreCase);
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            return UseFullTestConfiguration()
                ? new CombinatorialDataAttribute().GetData(testMethod)
                : new PairwiseDataAttribute().GetData(testMethod);
        }
    }
}
