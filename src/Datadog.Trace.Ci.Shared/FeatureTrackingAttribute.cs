using System;

namespace Datadog.Trace.Ci
{
    /// <summary>
    /// Expose a constant as a feature tracking value
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    internal class FeatureTrackingAttribute : Attribute
    {
    }
}
