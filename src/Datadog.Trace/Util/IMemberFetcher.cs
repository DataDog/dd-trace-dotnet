// From https://github.com/dotnet/runtime/blob/master/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/DiagnosticSourceEventSource.cs

using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Datadog.Trace.Util
{
    internal interface IMemberFetcher
    {
        /// <summary>
        /// Gets the value of the property on the specified object.
        /// </summary>
        /// <typeparam name="T">Type of the result.</typeparam>
        /// <param name="obj">The object that contains the property.</param>
        /// <returns>The value of the property on the specified object.</returns>
        public abstract T Fetch<T>(object obj);

        /// <summary>
        /// Gets the value of the property on the specified object.
        /// </summary>
        /// <typeparam name="T">Type of the result.</typeparam>
        /// <param name="obj">The object that contains the property.</param>
        /// <param name="objType">Type of the object</param>
        /// <returns>The value of the property on the specified object.</returns>
        public abstract T Fetch<T>(object obj, Type objType);
    }
}
