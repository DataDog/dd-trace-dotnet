using System;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.XUnit
{
    /// <summary>
    /// TestInvoker`1 structure
    /// </summary>
    [DuckCopy]
    public struct TestInvokerStruct
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
    }
}
