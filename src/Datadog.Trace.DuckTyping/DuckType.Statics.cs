using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck Type
    /// </summary>
    public static partial class DuckType
    {
        /// <summary>
        /// Gets the Type.GetTypeFromHandle method info
        /// </summary>
        public static readonly MethodInfo GetTypeFromHandleMethodInfo = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle));

        /// <summary>
        /// Gets the Enum.ToObject method info
        /// </summary>
        public static readonly MethodInfo EnumToObjectMethodInfo = typeof(Enum).GetMethod(nameof(Enum.ToObject), new[] { typeof(Type), typeof(object) });

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly object _locker = new object();
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, Lazy<CreateTypeResult>> DuckTypeCache = new ConcurrentDictionary<TypesTuple, Lazy<CreateTypeResult>>();
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentBag<DynamicMethod> DynamicMethods = new ConcurrentBag<DynamicMethod>();
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly PropertyInfo DuckTypeInstancePropertyInfo = typeof(IDuckType).GetProperty(nameof(IDuckType.Instance));
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly MethodInfo _methodBuilderGetToken = typeof(MethodBuilder).GetMethod("GetToken", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static ModuleBuilder _moduleBuilder = null;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static AssemblyBuilder _assemblyBuilder = null;
    }
}
