using System;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Attribute that indicates that this feature is in beta.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class BetaFeatureAttribute : Attribute
    {
    }
}
