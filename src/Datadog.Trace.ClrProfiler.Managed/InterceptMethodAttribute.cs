using System;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    ///
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class InterceptMethodAttribute : Attribute
    {
        /// <summary>
        ///
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        ///
        /// </summary>
        public InterceptMethodAttribute()
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="methodName"></param>
        public InterceptMethodAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }
}
