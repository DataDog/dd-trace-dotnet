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
using System.Threading;
// ReSharper disable InconsistentNaming

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck Type
    /// </summary>
    public static partial class DuckType
    {
        /// <summary>
        /// Synchronizes access to locker.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly object Locker;

        /// <summary>
        /// Stores cached duck type cache data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, Lazy<CreateTypeResult>> DuckTypeCache;

        /// <summary>
        /// Stores active builders.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly Dictionary<Assembly, ModuleBuilder> ActiveBuilders;

        /// <summary>
        /// Stores cached ignores access checks to assemblies set dictionary data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly Dictionary<ModuleBuilder, HashSet<string>> IgnoresAccessChecksToAssembliesSetDictionary;

        /// <summary>
        /// Stores get type from handle method info.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly MethodInfo? _getTypeFromHandleMethodInfo;

        /// <summary>
        /// Stores enum to object method info.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly MethodInfo? _enumToObjectMethodInfo;

        /// <summary>
        /// Stores duck type instance property info.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly PropertyInfo? _duckTypeInstancePropertyInfo;

        /// <summary>
        /// Stores method builder get token.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly MethodInfo? _methodBuilderGetToken;

        /// <summary>
        /// Stores ignores access checks to attribute ctor.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConstructorInfo? _ignoresAccessChecksToAttributeCtor;

        /// <summary>
        /// Stores assembly count.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static long _assemblyCount;

        /// <summary>
        /// Stores type count.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static long _typeCount;

        /// <summary>
        /// Stores runtime mode.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static int _runtimeMode;

        /// <summary>
        /// Stores runtime mode initialized.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static int _runtimeModeInitialized;

        static DuckType()
        {
            Locker = new();
            DuckTypeCache = new();
            ActiveBuilders = new();
            IgnoresAccessChecksToAssembliesSetDictionary = new();

            _getTypeFromHandleMethodInfo = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle));
            _enumToObjectMethodInfo = typeof(Enum).GetMethod(nameof(Enum.ToObject), new[] { typeof(Type), typeof(object) });
            _duckTypeInstancePropertyInfo = typeof(IDuckType).GetProperty(nameof(IDuckType.Instance));
            _methodBuilderGetToken = typeof(MethodBuilder).GetMethod("GetToken", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                  ?? typeof(MethodBuilder).GetProperty("MetadataToken", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
            _ignoresAccessChecksToAttributeCtor = typeof(IgnoresAccessChecksToAttribute).GetConstructor(new[] { typeof(string) });

            _assemblyCount = 0;
            _typeCount = 0;
            _runtimeMode = (int)DuckTypeRuntimeMode.Dynamic;
            _runtimeModeInitialized = 0;
        }

        /// <summary>
        /// Gets the Type.GetTypeFromHandle method info
        /// </summary>
        public static MethodInfo GetTypeFromHandleMethodInfo
        {
            get
            {
                if (_getTypeFromHandleMethodInfo is null)
                {
                    DuckTypeException.Throw($"{nameof(Type)}.{nameof(Type.GetTypeFromHandle)}() cannot be found.");
                }

                return _getTypeFromHandleMethodInfo;
            }
        }

        /// <summary>
        /// Gets the Enum.ToObject method info
        /// </summary>
        public static MethodInfo EnumToObjectMethodInfo
        {
            get
            {
                if (_enumToObjectMethodInfo is null)
                {
                    DuckTypeException.Throw($"{nameof(Enum)}.{nameof(Enum.ToObject)}() cannot be found.");
                }

                return _enumToObjectMethodInfo;
            }
        }

        /// <summary>
        /// Gets assembly count.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        internal static long AssemblyCount => _assemblyCount;

        /// <summary>
        /// Gets type count.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        internal static long TypeCount => _typeCount;

        /// <summary>
        /// Gets runtime mode.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        internal static DuckTypeRuntimeMode RuntimeMode => (DuckTypeRuntimeMode)Volatile.Read(ref _runtimeMode);

        private static PropertyInfo DuckTypeInstancePropertyInfo
        {
            get
            {
                if (_duckTypeInstancePropertyInfo is null)
                {
                    DuckTypeException.Throw($"{nameof(IDuckType)}.{nameof(IDuckType.Instance)} cannot be found.");
                }

                return _duckTypeInstancePropertyInfo;
            }
        }

        private static MethodInfo MethodBuilderGetToken
        {
            get
            {
                if (_methodBuilderGetToken is null)
                {
                    DuckTypeException.Throw($"{nameof(MethodBuilder)}.GetToken() cannot be found.");
                }

                return _methodBuilderGetToken;
            }
        }

        private static ConstructorInfo IgnoresAccessChecksToAttributeCtor
        {
            get
            {
                if (_ignoresAccessChecksToAttributeCtor is null)
                {
                    DuckTypeException.Throw($"{nameof(IgnoresAccessChecksToAttribute)}.ctor() cannot be found.");
                }

                return _ignoresAccessChecksToAttributeCtor;
            }
        }

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
        /// Gets get type from partial name.
        /// </summary>
        /// <param name="partialName">The partial name value.</param>
        /// <param name="throwOnError">The throw on error value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static Type? GetTypeFromPartialName(string partialName, bool throwOnError = false)
        {
            // We configure it to throw in case throwOnError is true and the partial name contains a version (is not partial).
            return Type.GetType(partialName, throwOnError: throwOnError && partialName.Contains("Version=")) ??
                   GetTypeFromPartialNameSlow(partialName, throwOnError);

            static Type? GetTypeFromPartialNameSlow(string partialName, bool throwOnError = false)
            {
                // If the type cannot be found, and the name doesn't contain a version,
                // we try to find the type in the current domain/alc using any assembly that has the same name.
                var typePair = partialName.Split([','], StringSplitOptions.RemoveEmptyEntries);
                if (typePair.Length != 2)
                {
                    if (throwOnError)
                    {
                        DuckTypeException.Throw($"Invalid type name: {partialName}");
                    }

                    return null;
                }

                var typeValue = typePair[0].Trim();
                var assemblyValue = typePair[1].Trim();

                try
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.GetName().Name != assemblyValue)
                        {
                            continue;
                        }

                        var type = assembly.GetType(typeValue, throwOnError: false);
                        if (type is not null)
                        {
                            return type;
                        }
                    }
                }
                catch
                {
                    if (throwOnError)
                    {
                        throw;
                    }
                }

                // If we were unable to load the type, and we have to throw an error, we do it now.
                if (throwOnError)
                {
                    DuckTypeException.Throw($"Type not found: {partialName}");
                }

                return null;
            }
        }

        /// <summary>
        /// DynamicMethods delegates cache
        /// </summary>
        /// <typeparam name="TProxyDelegate">Proxy delegate type</typeparam>
        public static class DelegateCache<TProxyDelegate>
            where TProxyDelegate : Delegate
        {
            /// <summary>
            /// Stores delegate.
            /// </summary>
            /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
            private static TProxyDelegate? _delegate;

            /// <summary>
            /// Get cached delegate from the DynamicMethod
            /// </summary>
            /// <returns>TProxyDelegate instance</returns>
            public static TProxyDelegate GetDelegate()
            {
                if (_delegate is null)
                {
                    DuckTypeException.Throw("Delegate instance in DelegateCache is null, please ensure that FillDelegate is called before this call.");
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
