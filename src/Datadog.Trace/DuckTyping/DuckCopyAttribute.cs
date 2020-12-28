using System;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck copy struct attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
    public class DuckCopyAttribute : Attribute
    {
    }
}
