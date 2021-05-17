// <copyright file="TestTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci
{
    /// <summary>
    /// Span tags for test data model
    /// </summary>
    internal static class TestTags
    {
        /// <summary>
        /// Test suite name
        /// </summary>
        [FeatureTracking]
        public const string Suite = "test.suite";

        /// <summary>
        /// Test name
        /// </summary>
        [FeatureTracking]
        public const string Name = "test.name";

        /// <summary>
        /// Test type
        /// </summary>
        [FeatureTracking]
        public const string Type = "test.type";

        /// <summary>
        /// Test type test
        /// </summary>
        public const string TypeTest = "test";

        /// <summary>
        /// Test type benchmark
        /// </summary>
        public const string TypeBenchmark = "benchmark";

        /// <summary>
        /// Test framework
        /// </summary>
        [FeatureTracking]
        public const string Framework = "test.framework";

        /// <summary>
        /// Test parameters
        /// </summary>
        [FeatureTracking]
        public const string Parameters = "test.parameters";

        /// <summary>
        /// Test traits
        /// </summary>
        [FeatureTracking]
        public const string Traits = "test.traits";

        /// <summary>
        /// Test status
        /// </summary>
        [FeatureTracking]
        public const string Status = "test.status";

        /// <summary>
        /// Test Pass status
        /// </summary>
        public const string StatusPass = "pass";

        /// <summary>
        /// Test Fail status
        /// </summary>
        public const string StatusFail = "fail";

        /// <summary>
        /// Test Skip status
        /// </summary>
        public const string StatusSkip = "skip";

        /// <summary>
        /// Test skip reason
        /// </summary>
        [FeatureTracking]
        public const string SkipReason = "test.skip_reason";

        /// <summary>
        /// Test output message
        /// </summary>
        [FeatureTracking]
        public const string Message = "test.message";

        /// <summary>
        /// Parameters metadata TestName
        /// </summary>
        public const string MetadataTestName = "test_name";

        /// <summary>
        /// Origin value for CIApp Test
        /// </summary>
        public const string CIAppTestOriginName = "ciapp-test";
    }
}
