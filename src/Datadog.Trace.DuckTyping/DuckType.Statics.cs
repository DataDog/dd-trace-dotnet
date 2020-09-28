using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        /// <summary>
        /// Gets the object.GetType() method info
        /// </summary>
        public static readonly MethodInfo ObjectGetTypeMethodInfo = typeof(object).GetMethod(nameof(object.GetType));

        /// <summary>
        /// Gets the DuckType.GetOrCreateProxyType() method info
        /// </summary>
        public static readonly MethodInfo GetOrCreateProxyTypeMethodInfo = typeof(DuckType).GetMethod(nameof(DuckType.GetOrCreateProxyType));

        /// <summary>
        /// Gets the CreateTypeResult.CreateInstance() method info
        /// </summary>
        public static readonly MethodInfo CreateInstanceMethodInfo = typeof(CreateTypeResult).GetMethod("CreateInstance");

        private const BindingFlags DefaultFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly object _locker = new object();
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly Dictionary<TypesTuple, CreateTypeResult> DuckTypeCache = new Dictionary<TypesTuple, CreateTypeResult>();
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentBag<DynamicMethod> DynamicMethods = new ConcurrentBag<DynamicMethod>();
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly MethodInfo DuckTypeCreateMethodInfo = typeof(DuckType).GetMethod(nameof(DuckType.Create), BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Type), typeof(object) }, null);
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly PropertyInfo DuckTypeInstancePropertyInfo = typeof(IDuckType).GetProperty(nameof(IDuckType.Instance));

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static ModuleBuilder _moduleBuilder = null;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static Func<DynamicMethod, RuntimeMethodHandle> _dynamicGetMethodDescriptor;
     }
}
