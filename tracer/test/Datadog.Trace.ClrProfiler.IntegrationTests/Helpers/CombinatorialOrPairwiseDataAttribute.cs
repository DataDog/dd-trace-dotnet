// <copyright file="CombinatorialOrPairwiseDataAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    public class CombinatorialOrPairwiseDataAttribute : DataAttribute
    {
        public static bool ShouldUseFullTestConfiguration(MethodInfo testMethod)
        {
            // if we don't need to run the full we can just return false right away
            if (!UseFullTestConfiguration())
            {
                return false;
            }

            // we know that files have changed, we can try to determine _which_ area(s) we need to test
            // ex: CHANGED_INTEGRATION_AREAS=AdoNet,Aws,Http -> AdoNet, Aws, Http
            var changedAreas = GetChangedIntegrationAreas();
            if (changedAreas.Length == 0)
            {
                // no specific area was changed (it may not yet be supported by this) run the full suite
                return true;
            }

            // we know that a specific area was changed, we can try to determine if this test is related to it
            var testType = testMethod.DeclaringType;
            if (testType == null)
            {
                // if we can't determine the test class, run the full suite (I don't think this can happen?)
                return true;
            }

            // if we know that a specific area was changed, we can try to determine if this test is related to it
            // ex: [IntegrationArea("AdoNet,Aws")] -> AdoNet, Aws
            var testedAreas = GetTestedAreas(testType);

            if (testedAreas.Count == 0)
            {
                return true;
            }

            return changedAreas.Any(changedArea => testedAreas.Contains(changedArea, StringComparer.OrdinalIgnoreCase));
        }

        public override IEnumerable<object?[]> GetData(MethodInfo testMethod)
        {
            return ShouldUseFullTestConfiguration(testMethod)
                ? new CombinatorialDataAttribute().GetData(testMethod)
                : new PairwiseDataAttribute().GetData(testMethod);
        }

        private static bool UseFullTestConfiguration()
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

        private static HashSet<string> GetTestedAreas(Type testType)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var attribute = testType.GetCustomAttribute<IntegrationAreaAttribute>();
            if (attribute != null)
            {
                foreach (var area in attribute.Areas)
                {
                    result.Add(area);
                }
            }

            return result;
        }

        private static string[] GetChangedIntegrationAreas()
        {
            var changedAreas = Environment.GetEnvironmentVariable("CHANGED_INTEGRATION_AREAS") ?? string.Empty;

            if (string.IsNullOrEmpty(changedAreas))
            {
                return [];
            }

            return changedAreas.Split([','], StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
