// <copyright file="TestTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Tags
{
    /// <summary>
    /// Span tags for test data model
    /// </summary>
    internal static class TestTags
    {
        /// <summary>
        /// Test suite name
        /// </summary>
        public const string Suite = "test.suite";

        /// <summary>
        /// Test name
        /// </summary>
        public const string Name = "test.name";

        /// <summary>
        /// Test type
        /// </summary>
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
        public const string Framework = "test.framework";

        /// <summary>
        /// Test framework version
        /// </summary>
        public const string FrameworkVersion = "test.framework_version";

        /// <summary>
        /// Test parameters
        /// </summary>
        public const string Parameters = "test.parameters";

        /// <summary>
        /// Test traits
        /// </summary>
        public const string Traits = "test.traits";

        /// <summary>
        /// Test status
        /// </summary>
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
        public const string SkipReason = "test.skip_reason";

        /// <summary>
        /// Test output message
        /// </summary>
        public const string Message = "test.message";

        /// <summary>
        /// Parameters metadata TestName
        /// </summary>
        public const string MetadataTestName = "test_name";

        /// <summary>
        /// Origin value for CIApp Test
        /// </summary>
        public const string CIAppTestOriginName = "ciapp-test";

        /// <summary>
        /// Library Language
        /// </summary>
        public const string Language = "language";

        /// <summary>
        /// CI Visibility Library Version
        /// </summary>
        public const string CILibraryVersion = "ci_library.version";

        /// <summary>
        /// The rate limit set during a performance / throughput test
        /// </summary>
        public const string RateLimit = "rate-limit";

        /// <summary>
        /// The payload size for a test
        /// </summary>
        public const string PayloadSize = "payload-size";
    }
}
