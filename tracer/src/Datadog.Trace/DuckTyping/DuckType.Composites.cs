// <copyright file="DuckType.Composites.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Composite interfaces (see <see cref="DuckTypeCompositeInterfaceAttribute"/>)
    /// </summary>
    public static partial class DuckType
    {
        /// <summary>
        /// The assembly of the composite interfaces: dynamic duck typing emits them in a dynamic assembly with this name, and a
        /// NativeAOT build generates it. The mappings dynamic duck typing records for them are then valid for the build.
        /// </summary>
        internal const string CompositeInterfacesAssemblyName = "Datadog.Trace.DuckType.Composites";

        /// <summary>
        /// The namespace of the composite interfaces.
        /// </summary>
        internal const string CompositeInterfacesNamespace = "Datadog.Trace.DuckTyping.Composites";

        private static readonly ConcurrentDictionary<string, Lazy<Type?>> CompositeInterfaces = new();
        private static readonly object CompositeInterfacesLock = new();
        private static ModuleBuilder? _compositeInterfacesModule;

        /// <summary>
        /// Gets the composite interface <paramref name="name"/>, declared with <see cref="DuckTypeCompositeInterfaceAttribute"/>,
        /// that inherits <paramref name="interfaces"/>. Without dynamic code (NativeAOT), it's the one the build generated, if any.
        /// </summary>
        /// <param name="name">The name of the composite interface.</param>
        /// <param name="interfaces">The interfaces it inherits (the types of the application, in the declaration's order).</param>
        /// <returns>The composite interface, or null when the build didn't generate it.</returns>
        internal static Type? GetCompositeInterface(string name, params Type[] interfaces)
        {
            // Different versions of the interfaces (version conflicts) get different interfaces: only the first gets the name
            // a NativeAOT build knows, which can only have one version.
            var key = name + "|" + string.Join("|", interfaces.Select(i => i.AssemblyQualifiedName));
            return CompositeInterfaces.GetOrAdd(key, _ => new Lazy<Type?>(() => CreateCompositeInterface(name, interfaces))).Value;
        }

        internal static bool IsCompositeInterface(Type type)
            => type.IsInterface && type.Namespace == CompositeInterfacesNamespace && type.Assembly.GetName().Name == CompositeInterfacesAssemblyName;

        private static Type? CreateCompositeInterface(string name, Type[] interfaces)
        {
            var fullName = $"{CompositeInterfacesNamespace}.{name}";
            if (!DuckTypeAotEngine.IsDynamicCodeSupported)
            {
                return Type.GetType($"{fullName}, {CompositeInterfacesAssemblyName}", throwOnError: false);
            }

            lock (CompositeInterfacesLock)
            {
                if (_compositeInterfacesModule is null)
                {
                    var assemblyName = new AssemblyName(CompositeInterfacesAssemblyName) { Version = typeof(DuckType).Assembly.GetName().Version };
                    _compositeInterfacesModule = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run).DefineDynamicModule("MainModule");
                }

                var typeName = fullName;
                for (var i = 2; _compositeInterfacesModule.GetType(typeName) is not null; i++)
                {
                    typeName = $"{fullName}_{i}";
                }

                var typeBuilder = _compositeInterfacesModule.DefineType(
                    typeName,
                    TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract,
                    parent: null,
                    interfaces: interfaces);
                return typeBuilder.CreateTypeInfo()!.AsType();
            }
        }
    }
}
