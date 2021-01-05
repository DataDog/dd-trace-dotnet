using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.XUnit
{
    /// <summary>
    /// TestRunner`1 structure
    /// </summary>
    [DuckCopy]
    public struct TestRunnerStruct
    {
        /// <summary>
        /// Test class Type
        /// </summary>
        public Type TestClass;

        /// <summary>
        /// Test method MethodInfo
        /// </summary>
        public MethodInfo TestMethod;

        /// <summary>
        /// Test method arguments
        /// </summary>
        public object[] TestMethodArguments;

        /// <summary>
        /// Test case
        /// </summary>
        public TestCaseStruct TestCase;

        /// <summary>
        /// Exception aggregator
        /// </summary>
        public IExceptionAggregator Aggregator;

        /// <summary>
        /// Skip reason
        /// </summary>
        public string SkipReason;
    }
}
