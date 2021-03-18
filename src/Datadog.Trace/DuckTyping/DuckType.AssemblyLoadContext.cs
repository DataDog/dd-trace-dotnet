using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
#if HAVE_ASSEMBLYLOADCONTEXT
using RuntimeLoader = System.Runtime.Loader;
#endif

#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1214
#pragma warning disable SA1203 // Constants should appear before fields

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck Type
    /// </summary>
    public static partial class DuckType
    {
        /// <summary>
        /// Contains the current module builders, the key is an object that can be an AssemblyLoadContext (NETCORE) or an Assembly (NETFRAMEWORK) instance.
        /// The usage of the ConditionalWeakTable is due to the fact that an AssemblyLoadContext can be unloaded.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConditionalWeakTable<object, ModuleBuilder> ModuleBuilders = new ConditionalWeakTable<object, ModuleBuilder>();

        /// <summary>
        /// Count of emmited AssemblyBuilders.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static long _assemblyCount = 0;

#if HAVE_ASSEMBLYLOADCONTEXT || IS_NETSTANDARD2_X
        /// <summary>
        /// Contains the `DefineDynamicAssemblyDelegate` created on the target context. The key is an AssemblyLoadContext object.
        /// The usage of the ConditionalWeakTable is due to the fact that an AssemblyLoadContext can be unloaded.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConditionalWeakTable<object, DefineDynamicAssemblyDelegate> DefineDynamicAssemblies = new ConditionalWeakTable<object, DefineDynamicAssemblyDelegate>();

        /// <summary>
        /// Context where the DuckType class is loaded.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static object _currentContext;

        /// <summary>
        /// Delegate used to call AssemblyBuilder.DefineDynamicAssembly method in a target context.
        /// </summary>
        /// <param name="name">Assembly name</param>
        /// <returns>Assembly builder instance</returns>
        private delegate AssemblyBuilder DefineDynamicAssemblyDelegate(string name);

        /// <summary>
        /// Gets the target context where the Assembly builder should be defined given a target type
        /// </summary>
        /// <param name="targetType">Target type for ducktyping</param>
        /// <returns>An AssemblyLoadContext object</returns>
        private static object GetTargetContext(Type targetType)
        {
            // First we get the AssemblyLoadContext of the target type
            // If the Type is a Generic type, we scan for the contexts of the generics parameters (eg: Task<T>, Dictioanry<TKey, TValue>)
            // If we get different contexts we remove the target type context if the target type is from System.Private.CoreLib
            // If only 1 context remains then we use that. (Context of T from Task<T>)
            // If we still have multiple context, then we play safe and create a new isolated context for that combination (context of TKey being different to context of TValue in a Dictionary<TKey, TValue>)
            List<object> contexts = new List<object>();
            var targetContext = GetAssemblyLoadContext(targetType.Assembly ?? typeof(DuckType).Assembly);
            contexts.Add(targetContext);

            if (targetType.IsGenericType)
            {
                foreach (var type in targetType.GetGenericArguments())
                {
                    var context = GetAssemblyLoadContext(type.Assembly);
                    if (targetContext != context && !contexts.Contains(context))
                    {
                        contexts.Add(context);
                    }
                }

                if (contexts.Count > 1 && targetType.Assembly == typeof(object).Assembly)
                {
                    contexts.Remove(targetContext);
                }
            }

            if (contexts.Count > 1)
            {
                return CreateAssemblyLoadContext($"DuckType-For:{targetType.Name}");
            }

            return contexts[0];
        }

        /// <summary>
        /// Creates an Assembly builder with a name in a target context
        /// </summary>
        /// <param name="name">Name of the assembly</param>
        /// <param name="targetContext">AssemblyLoadContext where the assembly is going to be defined.</param>
        /// <returns>An AssemblyBuilder instance</returns>
        private static AssemblyBuilder CreateAssemblyBuilder(string name, object targetContext)
        {
            if (_currentContext == null)
            {
                _currentContext = GetAssemblyLoadContext(typeof(DuckType).Assembly);
            }

            if (targetContext != _currentContext)
            {
                // if the target context is different from the current context we need to call the `AssemblyBuilder.DefineDynamicAssembly` from
                // the target context, for that we load the `Datadog.Trace.AssemblyLoadContext` assembly into the target context and create a delegate
                // to `Datadog.Trace.AssemblyLoadContext.AssemblyBuilderHelper.DefineDynamicAssembly` method and call it.
                if (!DefineDynamicAssemblies.TryGetValue(targetContext, out var @delegate))
                {
                    var loaderAssembly = LoadFromAssemblyPath(targetContext, typeof(AssemblyLoadContext.AssemblyBuilderHelper).Assembly.Location);
                    var loaderType = loaderAssembly.GetType(typeof(AssemblyLoadContext.AssemblyBuilderHelper).FullName);
                    var loaderMethod = loaderType.GetMethod(nameof(AssemblyLoadContext.AssemblyBuilderHelper.DefineDynamicAssembly), BindingFlags.Static | BindingFlags.Public);
                    @delegate = (DefineDynamicAssemblyDelegate)loaderMethod.CreateDelegate(typeof(DefineDynamicAssemblyDelegate));
                    DefineDynamicAssemblies.Add(targetContext, @delegate);
                }

                return @delegate(name);
            }

            // In the case we are in the same context we just call `AssemblyBuilder.DefineDynamicAssembly` from here.
            return AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        }
#endif

#if HAVE_ASSEMBLYLOADCONTEXT
        // ***
        // Indirections to the AssemblyLoadContext methods.
        // ***
        private static object GetAssemblyLoadContext(Assembly assembly)
        {
            return RuntimeLoader.AssemblyLoadContext.GetLoadContext(assembly);
        }

        private static Assembly LoadFromAssemblyPath(object context, string path)
        {
            return ((RuntimeLoader.AssemblyLoadContext)context).LoadFromAssemblyPath(path);
        }

        private static object CreateAssemblyLoadContext(string name)
        {
            return new RuntimeLoader.AssemblyLoadContext(name);
        }

        /// <summary>
        /// Gets the ModuleBuilder instance from a target type.
        /// By resolving all the AssemblyLoadContext if is required.
        /// </summary>
        /// <param name="targetType">Target type for ducktyping</param>
        /// <returns>ModuleBuilder instance</returns>
        private static ModuleBuilder GetModuleBuilder(Type targetType)
        {
            object targetContext = GetTargetContext(targetType);
            if (!ModuleBuilders.TryGetValue(targetContext, out var moduleBuilder))
            {
                var assemblyBuilder = CreateAssemblyBuilder($"DuckTypeAssembly_{++_assemblyCount}", targetContext);
                moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
                ModuleBuilders.Add(targetContext, moduleBuilder);
            }

            return moduleBuilder;
        }
#elif IS_NETSTANDARD2_X
        // ***
        // Proxies to the AssemblyLoadContext methods using delegates and fetchers.
        // ***
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const string AssemblyLoadContextTypeName = "System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static Type _assemblyLoadContextType = null;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static ContextFetcher _contextFetcher = null;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static LoadFromAssemblyPathFetcher _loadFromAssemblyPathFetcher = null;

        private static object GetAssemblyLoadContext(Assembly assembly)
        {
            if (_contextFetcher == null)
            {
                MethodInfo mInfo = _assemblyLoadContextType.GetMethod("GetLoadContext", BindingFlags.Static | BindingFlags.Public);
                _contextFetcher = ContextFetcher.GetFetcher(mInfo);
            }

            return _contextFetcher.Get(assembly);
        }

        private static Assembly LoadFromAssemblyPath(object context, string path)
        {
            if (_loadFromAssemblyPathFetcher == null)
            {
                MethodInfo loadFromAssemblyPathMethod = _assemblyLoadContextType.GetMethod("LoadFromAssemblyPath", BindingFlags.Public | BindingFlags.Instance);
                _loadFromAssemblyPathFetcher = LoadFromAssemblyPathFetcher.GetFetcher(loadFromAssemblyPathMethod, context);
            }

            return _loadFromAssemblyPathFetcher.Load(context, path);
        }

        private static object CreateAssemblyLoadContext(string name)
        {
            return Activator.CreateInstance(_assemblyLoadContextType, name);
        }

        /// <summary>
        /// Gets the ModuleBuilder instance from a target type.
        /// By resolving all the AssemblyLoadContext if is required.
        /// </summary>
        /// <param name="targetType">Target type for ducktyping</param>
        /// <returns>ModuleBuilder instance</returns>
        private static ModuleBuilder GetModuleBuilder(Type targetType)
        {
            ModuleBuilder moduleBuilder;

            if (_assemblyLoadContextType == null)
            {
                _assemblyLoadContextType = Type.GetType(AssemblyLoadContextTypeName, throwOnError: false);

                if (_assemblyLoadContextType == null)
                {
                    object targetAssembly = targetType.Assembly ?? typeof(DuckType).Assembly;
                    if (!ModuleBuilders.TryGetValue(targetAssembly, out moduleBuilder))
                    {
                        var assemblyBuilder = CreateAssemblyBuilderFallback($"DuckTypeAssembly.{targetType.Assembly?.GetName().Name}_{++_assemblyCount}", targetType);
                        moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
                        ModuleBuilders.Add(targetAssembly, moduleBuilder);
                    }

                    return moduleBuilder;
                }
            }

            object targetContext = GetTargetContext(targetType);
            if (!ModuleBuilders.TryGetValue(targetContext, out moduleBuilder))
            {
                var assemblyBuilder = CreateAssemblyBuilder($"DuckTypeAssembly.{targetType.Assembly?.GetName().Name}_{++_assemblyCount}", targetContext);
                moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
                ModuleBuilders.Add(targetContext, moduleBuilder);
            }

            return moduleBuilder;
        }

        /// <summary>
        /// Fallback method in case `System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader` cannot be loaded.
        /// </summary>
        /// <param name="name">Name of the assembly</param>
        /// <param name="targetType">Target type for ducktyping</param>
        /// <returns>An AssemblyBuilder instance</returns>
        private static AssemblyBuilder CreateAssemblyBuilderFallback(string name, Type targetType)
        {
            return AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        }

        private class ContextFetcher
        {
            private delegate TReturn GetAssemblyLoadContextDelegate<TReturn>(Assembly assembly);

            public static ContextFetcher GetFetcher(MethodInfo minfo)
            {
                return (ContextFetcher)Activator.CreateInstance(typeof(GenericContextFetcher<>).MakeGenericType(minfo.ReturnType), minfo);
            }

            public virtual object Get(Assembly assembly)
            {
                return null;
            }

            private class GenericContextFetcher<TReturn> : ContextFetcher
            {
                private GetAssemblyLoadContextDelegate<TReturn> _delegate;

                public GenericContextFetcher(MethodInfo mInfo)
                {
                    _delegate = (GetAssemblyLoadContextDelegate<TReturn>)mInfo.CreateDelegate(typeof(GetAssemblyLoadContextDelegate<TReturn>));
                }

                public override object Get(Assembly assembly)
                {
                    return _delegate(assembly);
                }
            }
        }

        private class LoadFromAssemblyPathFetcher
        {
            private delegate Assembly LoadFromAssemblyPathDelegate<TContext>(TContext context, string path);

            public static LoadFromAssemblyPathFetcher GetFetcher(MethodInfo minfo, object context)
            {
                return (LoadFromAssemblyPathFetcher)Activator.CreateInstance(typeof(GenericLoadFromAssemblyPathFetcher<>).MakeGenericType(context.GetType().BaseType), minfo);
            }

            public virtual Assembly Load(object context, string path)
            {
                return null;
            }

            private class GenericLoadFromAssemblyPathFetcher<TContext> : LoadFromAssemblyPathFetcher
            {
                private LoadFromAssemblyPathDelegate<TContext> _delegate;

                public GenericLoadFromAssemblyPathFetcher(MethodInfo mInfo)
                {
                    _delegate = (LoadFromAssemblyPathDelegate<TContext>)mInfo.CreateDelegate(typeof(LoadFromAssemblyPathDelegate<TContext>));
                }

                public override Assembly Load(object context, string path)
                {
                    if (context is TContext ctx)
                    {
                        return _delegate(ctx, path);
                    }

                    return null;
                }
            }
        }
#else
        /// <summary>
        /// Creates an assembly builder with a Name. (.NET Framework / Non AssemblyLoadContext version)
        /// </summary>
        /// <param name="name">Name of the assembly</param>
        /// <param name="targetType">Target type for ducktyping</param>
        /// <returns>An AssemblyBuilder instance</returns>
        private static AssemblyBuilder CreateAssemblyBuilder(string name, Type targetType)
        {
            return AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        }

        /// <summary>
        /// Gets the ModuleBuilder instance from a target type.  (.NET Framework / Non AssemblyLoadContext version)
        /// </summary>
        /// <param name="targetType">Target type for ducktyping</param>
        /// <returns>ModuleBuilder instance</returns>
        private static ModuleBuilder GetModuleBuilder(Type targetType)
        {
            object targetAssembly = targetType.Assembly ?? typeof(DuckType).Assembly;
            if (!ModuleBuilders.TryGetValue(targetAssembly, out var moduleBuilder))
            {
                var assemblyBuilder = CreateAssemblyBuilder($"DuckTypeAssembly.{targetType.Assembly?.GetName().Name}_{++_assemblyCount}", targetType);
                moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
                ModuleBuilders.Add(targetAssembly, moduleBuilder);
            }

            return moduleBuilder;
        }
#endif
    }
}
