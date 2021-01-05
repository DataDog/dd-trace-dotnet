using System;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MsTestV2
{
    /// <summary>
    /// TestMethod ducktype interface
    /// </summary>
    public interface ITestMethod
    {
        /// <summary>
        /// Gets the test method name
        /// </summary>
        string TestMethodName { get; }

        /// <summary>
        /// Gets the test class name
        /// </summary>
        string TestClassName { get; }

        /// <summary>
        /// Gets the MethodInfo
        /// </summary>
        MethodInfo MethodInfo { get; }

        /// <summary>
        /// Gets the test arguments
        /// </summary>
        object[] Arguments { get; }

        /// <summary>
        /// Gets all attributes
        /// </summary>
        /// <param name="inherit">Injerits all the attributes from base classes</param>
        /// <returns>Attribute array</returns>
        Attribute[] GetAllAttributes(bool inherit);
    }
}
