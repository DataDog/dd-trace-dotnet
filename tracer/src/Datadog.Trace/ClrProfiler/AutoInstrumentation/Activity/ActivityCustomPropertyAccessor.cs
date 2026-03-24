// <copyright file="ActivityCustomPropertyAccessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// Provides zero-allocation cached delegates for <c>Activity.GetCustomProperty</c> and
    /// <c>Activity.SetCustomProperty</c> (available on DiagnosticSource 5.0+).
    /// <para>
    /// The static fields are initialized once per <typeparamref name="TTarget"/> type (i.e., once per
    /// concrete Activity version encountered), after which every call is a direct delegate invocation
    /// with no heap allocation.
    /// </para>
    /// </summary>
    /// <typeparam name="TTarget">The concrete Activity type (monomorphized by the JIT per CallTarget site).</typeparam>
    internal static class ActivityCustomPropertyAccessor<TTarget>
    {
        private const string SpanPropertyKey = "__dd_span__";
        private const string InitialOpNameKey = "__dd_initial_op__";

        /// <summary>
        /// Open-instance delegate for <c>Activity.GetCustomProperty(string)</c>.
        /// Null when the target type does not expose the method (DiagnosticSource &lt; 5.0).
        /// </summary>
        public static readonly Func<TTarget, string, object?>? GetCustomProperty = CreateGetDelegate();

        /// <summary>
        /// Wrapper delegate for <c>Activity.SetCustomProperty(string, object?)</c> that discards the return value.
        /// Null when the target type does not expose the method (DiagnosticSource &lt; 5.0).
        /// </summary>
        public static readonly Action<TTarget, string, object?>? SetCustomProperty = CreateSetDelegate();

        /// <summary>
        /// Retrieves the <see cref="Scope"/> stored on the activity via <see cref="SpanPropertyKey"/>.
        /// Returns null if the activity is not tracked by this integration or if the delegate is unavailable.
        /// </summary>
        public static Scope? GetScope(TTarget instance)
            => GetCustomProperty?.Invoke(instance, SpanPropertyKey) as Scope;

        /// <summary>
        /// Stores the given <paramref name="scope"/> on the activity under <see cref="SpanPropertyKey"/>.
        /// </summary>
        public static void SetScope(TTarget instance, Scope? scope)
            => SetCustomProperty?.Invoke(instance, SpanPropertyKey, scope);

        /// <summary>
        /// Retrieves the initial operation name saved at Activity.Start() time.
        /// Used by <c>ActivityStopIntegration</c> to detect whether the user explicitly
        /// overrode the operation name via an "operation.name" tag.
        /// </summary>
        public static string? GetInitialOperationName(TTarget instance)
            => GetCustomProperty?.Invoke(instance, InitialOpNameKey) as string;

        /// <summary>
        /// Stores the initial operation name on the activity.
        /// </summary>
        public static void SetInitialOperationName(TTarget instance, string? operationName)
            => SetCustomProperty?.Invoke(instance, InitialOpNameKey, operationName);

        private static Func<TTarget, string, object?>? CreateGetDelegate()
        {
            try
            {
                var method = typeof(TTarget).GetMethod(
                    "GetCustomProperty",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string) },
                    null);

                if (method is null)
                {
                    return null;
                }

                // Create an open-instance delegate: Func<TTarget, string, object?>
                return (Func<TTarget, string, object?>)Delegate.CreateDelegate(
                    typeof(Func<TTarget, string, object?>),
                    firstArgument: null,
                    method);
            }
            catch
            {
                return null;
            }
        }

        private static Action<TTarget, string, object?>? CreateSetDelegate()
        {
            try
            {
                var method = typeof(TTarget).GetMethod(
                    "SetCustomProperty",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string), typeof(object) },
                    null);

                if (method is null)
                {
                    return null;
                }

                // Activity.SetCustomProperty returns Activity, but we want Action<TTarget, string, object?>.
                // Use a DynamicMethod wrapper that calls the method and discards the return value.
                var dm = new DynamicMethod(
                    "SetCustomPropertyWrapper",
                    returnType: null,
                    parameterTypes: new[] { typeof(TTarget), typeof(string), typeof(object) },
                    typeof(ActivityCustomPropertyAccessor<TTarget>).Module,
                    skipVisibility: true);

                var il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);       // TTarget instance
                il.Emit(OpCodes.Ldarg_1);       // string propertyName
                il.Emit(OpCodes.Ldarg_2);       // object? propertyValue
                il.Emit(OpCodes.Callvirt, method);

                if (method.ReturnType != typeof(void))
                {
                    il.Emit(OpCodes.Pop);       // discard non-void return value
                }

                il.Emit(OpCodes.Ret);

                return (Action<TTarget, string, object?>)dm.CreateDelegate(typeof(Action<TTarget, string, object?>));
            }
            catch
            {
                return null;
            }
        }
    }
}
