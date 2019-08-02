using System;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.EntrypointManaged
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace.ClrProfiler.Managed .NET assembly.
    /// </summary>
    public class LoadHelper
    {
        /// <summary>
        /// Initializes static members of the <see cref="LoadHelper"/> class.
        /// This method also attempts to load the Datadog.Trace.ClrProfiler.Managed. NET assembly.
        /// </summary>
        static LoadHelper()
        {
            try
            {
                Assembly.Load(new AssemblyName("Datadog.Trace.ClrProfiler.Managed, Version=1.6.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb"));
            }
            catch
            {
            }
        }
    }
}
