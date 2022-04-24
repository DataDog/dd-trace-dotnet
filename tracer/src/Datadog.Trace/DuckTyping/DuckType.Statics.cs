// <copyright file="DuckType.Statics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck Type
    /// </summary>
    public static partial class DuckType
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly object Locker;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, Lazy<CreateTypeResult>> DuckTypeCache;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly Dictionary<Assembly, ModuleBuilder> ActiveBuilders;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly Dictionary<ModuleBuilder, HashSet<string>> IgnoresAccessChecksToAssembliesSetDictionary;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly PropertyInfo DuckTypeInstancePropertyInfo;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly MethodInfo MethodBuilderGetToken;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConstructorInfo IgnoresAccessChecksToAttributeCtor;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static long _assemblyCount;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static long _typeCount;

        /// <summary>
        /// Gets the Type.GetTypeFromHandle method info
        /// </summary>
        public static readonly MethodInfo GetTypeFromHandleMethodInfo;

        /// <summary>
        /// Gets the Enum.ToObject method info
        /// </summary>
        public static readonly MethodInfo EnumToObjectMethodInfo;

        static DuckType()
        {
            Locker = new();
            DuckTypeCache = new();
            ActiveBuilders = new();
            IgnoresAccessChecksToAssembliesSetDictionary = new();

            if (typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle)) is { } getTypeFromHandleMethodInfo)
            {
                GetTypeFromHandleMethodInfo = getTypeFromHandleMethodInfo;
            }
            else
            {
                throw new DuckTypeException($"{nameof(Type)}.{nameof(Type.GetTypeFromHandle)}() cannot be found.");
            }

            if (typeof(Enum).GetMethod(nameof(Enum.ToObject), new[] { typeof(Type), typeof(object) }) is { } enumToObjectMethodInfo)
            {
                EnumToObjectMethodInfo = enumToObjectMethodInfo;
            }
            else
            {
                throw new DuckTypeException($"{nameof(Enum)}.{nameof(Enum.ToObject)}() cannot be found.");
            }

            if (typeof(IDuckType).GetProperty(nameof(IDuckType.Instance)) is { } duckTypeInstancePropertyInfo)
            {
                DuckTypeInstancePropertyInfo = duckTypeInstancePropertyInfo;
            }
            else
            {
                throw new DuckTypeException($"{nameof(IDuckType)}.{nameof(IDuckType.Instance)} cannot be found.");
            }

            if (typeof(MethodBuilder).GetMethod("GetToken", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is { } methodBuilderGetToken)
            {
                MethodBuilderGetToken = methodBuilderGetToken;
            }
            else
            {
                throw new DuckTypeException($"{nameof(MethodBuilder)}.GetToken() cannot be found.");
            }

            if (typeof(IgnoresAccessChecksToAttribute).GetConstructor(new[] { typeof(string) }) is { } ignoresAccessChecksToAttributeCtor)
            {
                IgnoresAccessChecksToAttributeCtor = ignoresAccessChecksToAttributeCtor;
            }
            else
            {
                throw new DuckTypeException($"{nameof(IgnoresAccessChecksToAttribute)}.ctor() cannot be found.");
            }

            _assemblyCount = 0;
            _typeCount = 0;
        }

        internal static long AssemblyCount => _assemblyCount;

        internal static long TypeCount => _typeCount;

        /// <summary>
        /// Gets the ModuleBuilder instance from a target type.  (.NET Framework / Non AssemblyLoadContext version)
        /// </summary>
        /// <param name="targetType">Target type for ducktyping</param>
        /// <param name="isVisible">Is visible boolean</param>
        /// <returns>ModuleBuilder instance</returns>
        private static ModuleBuilder GetModuleBuilder(Type targetType, bool isVisible)
        {
            Assembly targetAssembly = targetType.Assembly;

            if (!isVisible)
            {
                // If the target type is not visible then we create a new module builder.
                // This is the only way to IgnoresAccessChecksToAttribute to work.
                // We can't reuse the module builder if the attributes collection changes.
                return CreateModuleBuilder(DuckTypeConstants.DuckTypeNotVisibleAssemblyPrefix + targetType.Name, targetAssembly);
            }

            if (targetType.IsGenericType)
            {
                foreach (var type in targetType.GetGenericArguments())
                {
                    if (type.Assembly != targetAssembly)
                    {
                        return CreateModuleBuilder(DuckTypeConstants.DuckTypeGenericTypeAssemblyPrefix + targetType.Name, targetAssembly);
                    }
                }
            }

            if (!ActiveBuilders.TryGetValue(targetAssembly, out var moduleBuilder))
            {
                moduleBuilder = CreateModuleBuilder(DuckTypeConstants.DuckTypeAssemblyPrefix + targetType.Assembly.GetName().Name, targetAssembly);
                ActiveBuilders.Add(targetAssembly, moduleBuilder);
            }

            return moduleBuilder;

            static ModuleBuilder CreateModuleBuilder(string name, Assembly targetAssembly)
            {
                var assemblyName = new AssemblyName(name + $"_{++_assemblyCount}");
                assemblyName.Version = targetAssembly.GetName().Version;
                var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                return assemblyBuilder.DefineDynamicModule("MainModule");
            }
        }

        /// <summary>
        /// DynamicMethods delegates cache
        /// </summary>
        /// <typeparam name="TProxyDelegate">Proxy delegate type</typeparam>
        public static class DelegateCache<TProxyDelegate>
            where TProxyDelegate : Delegate
        {
            private static TProxyDelegate? _delegate;

            /// <summary>
            /// Get cached delegate from the DynamicMethod
            /// </summary>
            /// <returns>TProxyDelegate instance</returns>
            public static TProxyDelegate GetDelegate()
            {
                if (_delegate is null)
                {
                    throw new DuckTypeException("Delegate instance in DelegateCache is null, please ensure that FillDelegate is called before this call.");
                }

                return _delegate;
            }

            /// <summary>
            /// Create delegate from a DynamicMethod index
            /// </summary>
            /// <param name="index">Dynamic method index</param>
            internal static void FillDelegate(int index)
            {
                _delegate = (TProxyDelegate)ILHelpersExtensions.GetDynamicMethodForIndex(index)
                    .CreateDelegate(typeof(TProxyDelegate));
            }
        }
    }
}
