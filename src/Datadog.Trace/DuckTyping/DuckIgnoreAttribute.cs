using System;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Ignores the member when DuckTyping
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = false)]
    public class DuckIgnoreAttribute : Attribute
    {
    }
}
