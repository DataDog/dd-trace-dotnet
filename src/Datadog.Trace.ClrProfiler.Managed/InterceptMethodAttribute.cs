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
        public string Integration { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string CallerAssembly { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string CallerType { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string CallerMethod { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string TargetAssembly { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string TargetType { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string TargetMethod { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string TargetSignature { get; set; }
    }
}
