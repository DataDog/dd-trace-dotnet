namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.Test
    /// </summary>
    public interface ITest
    {
        /// <summary>
        /// Gets a MethodInfo for the method implementing this test.
        /// Returns null if the test is not implemented as a method.
        /// </summary>
        IMethodInfo Method { get; }

        /// <summary>
        /// Gets the arguments to use in creating the test or empty array if none required.
        /// </summary>
        object[] Arguments { get; }

        /// <summary>
        /// Gets the properties for this test
        /// </summary>
        IPropertyBag Properties { get; }
    }
}
