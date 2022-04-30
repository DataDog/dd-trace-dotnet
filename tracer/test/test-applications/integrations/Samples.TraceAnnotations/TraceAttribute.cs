#nullable enable

using System;

namespace Datadog.Trace.Annotations
{
    /// <summary>
    /// Custom attribute whose fullname matches the official Datadog.Trace.Annotations.TraceAttribute
    /// 
    /// The Datadog automatic instrumentation automatically recognizes this well-known
    /// type to enable instrumentation of arbitrary methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TraceAttribute : Attribute
    {
        public string? OperationName { get; set; }

        public string? ResourceName { get; set; }
    }
}
