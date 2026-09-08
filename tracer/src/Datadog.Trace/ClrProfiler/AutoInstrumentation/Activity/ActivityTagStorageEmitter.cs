// <copyright file="ActivityTagStorageEmitter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// Synthesises a real <c>System.Diagnostics.Activity.Enumerator&lt;KeyValuePair&lt;string, object?&gt;&gt;</c>
    /// over a <c>System.Diagnostics.DiagNode&lt;T&gt;</c> chain built from a Datadog <see cref="Span"/>'s
    /// tags, for <c>Activity.EnumerateTagObjects()</c> (DiagnosticSource 9.0+).
    /// <para>
    /// The CallTarget rewrite fixes the intercepted method's IL return type to that exact
    /// <c>Enumerator&lt;T&gt;</c> struct, so we cannot hand back a different shape — instead we build a
    /// real one, over nodes we own, and let it walk them like any other <c>DiagNode</c> chain.
    /// </para>
    /// <para>
    /// Resolved once per <typeparamref name="TTarget"/> (i.e. once per concrete Activity type / DS version
    /// encountered) via two cached <see cref="DynamicMethod"/>s (<c>skipVisibility: true</c>), following the
    /// same reflect-once-and-cache shape as <see cref="ActivityCustomPropertyAccessor{TTarget}"/>. If
    /// DiagnosticSource's internal layout doesn't match what we expect here — a member missing, or a field
    /// typed differently — <see cref="IsAvailable"/> is false and callers must fall back to not skipping
    /// the intercepted method's body, rather than throwing.
    /// </para>
    /// </summary>
    /// <typeparam name="TTarget">The concrete Activity type (monomorphized by the JIT per CallTarget site).</typeparam>
    internal static class ActivityTagStorageEmitter<TTarget>
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityTagStorageEmitter<TTarget>));

        private static readonly CreateNodeDelegate? CreateNode;
        private static readonly CreateEnumeratorDelegate? CreateEnumerator;
        private static readonly Type? EnumeratorType;

        static ActivityTagStorageEmitter()
        {
            try
            {
                var result = Build();
                CreateNode = result.CreateNode;
                CreateEnumerator = result.CreateEnumerator;
                EnumeratorType = result.EnumeratorType;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to resolve DiagnosticSource's internal tag-storage shape for {TargetType}; EnumerateTagObjects() will not be intercepted", typeof(TTarget));
            }
        }

        private delegate object CreateNodeDelegate(KeyValuePair<string, object?> value, object? next);

        private delegate object CreateEnumeratorDelegate(object? headNode);

        /// <summary>
        /// Gets a value indicating whether this type successfully resolved DiagnosticSource's internal
        /// <c>DiagNode&lt;T&gt;</c> / <c>Activity.Enumerator&lt;T&gt;</c> shape for
        /// <typeparamref name="TTarget"/>'s assembly. False means the internal layout is not what we
        /// expect; callers must not skip the intercepted method's body.
        /// </summary>
        public static bool IsAvailable => CreateNode is not null && CreateEnumerator is not null;

        /// <summary>
        /// Builds a real <c>Activity.Enumerator&lt;KeyValuePair&lt;string, object?&gt;&gt;</c> over a
        /// freshly-synthesised <c>DiagNode</c> chain holding <paramref name="tags"/>, boxed as
        /// <typeparamref name="TReturn"/> (the exact closed <c>Enumerator&lt;T&gt;</c> struct the rewritten
        /// method must return). Returns false if <see cref="IsAvailable"/> is false, or if
        /// <typeparamref name="TReturn"/> unexpectedly isn't the type resolved for
        /// <typeparamref name="TTarget"/> — a caller must treat either as "don't skip the method body".
        /// Deliberately a <c>bool</c> + <c>out</c> rather than a nullable return: <typeparamref name="TReturn"/>
        /// is an unconstrained generic parameter bound to a value type at this call site, and
        /// <c>default(TReturn)</c> for a struct is a real (zero-valued) value, not something an
        /// <c>is not null</c> check on the caller's side could distinguish from success.
        /// </summary>
        public static bool TryEnumerate<TReturn>(IReadOnlyList<KeyValuePair<string, object?>> tags, out TReturn result)
        {
            if (CreateNode is null || CreateEnumerator is null)
            {
                result = default!;
                return false;
            }

            if (EnumeratorType != typeof(TReturn))
            {
                Log.Error("EnumerateTagObjects() return type mismatch for {TargetType}. Expected {Expected} but the call site expects {Actual}", typeof(TTarget), EnumeratorType!, typeof(TReturn));
                result = default!;
                return false;
            }

            object? node = null;
            for (var i = tags.Count - 1; i >= 0; i--)
            {
                node = CreateNode(tags[i], node);
            }

            result = (TReturn)CreateEnumerator(node);
            return true;
        }

        private static BuildResult Build()
        {
            var assembly = typeof(TTarget).Assembly;
            var kvpType = typeof(KeyValuePair<string, object?>);

            var diagNodeOpenType = assembly.GetType("System.Diagnostics.DiagNode`1", throwOnError: false);
            var enumeratorOpenType = typeof(TTarget).GetNestedType("Enumerator`1", BindingFlags.Public | BindingFlags.NonPublic);

            if (diagNodeOpenType is null || enumeratorOpenType is null)
            {
                Log.Error("Could not resolve DiagNode`1 or Activity.Enumerator`1 on {TargetType}'s assembly; EnumerateTagObjects() will not be intercepted", typeof(TTarget));
                return default;
            }

            var diagNodeType = diagNodeOpenType.MakeGenericType(kvpType);
            var enumeratorType = enumeratorOpenType.MakeGenericType(kvpType);

            var nodeCtor = diagNodeType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { kvpType }, null);
            var nextField = diagNodeType.GetField("Next", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var enumeratorCtor = enumeratorType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { diagNodeType }, null);

            if (nodeCtor is null || nextField is null || enumeratorCtor is null || nextField.FieldType != diagNodeType)
            {
                Log.Error("DiagNode`1 or Activity.Enumerator`1 on {TargetType}'s assembly did not have the expected shape; EnumerateTagObjects() will not be intercepted", typeof(TTarget));
                return default;
            }

            var createNode = BuildCreateNodeDelegate(diagNodeType, nodeCtor, nextField, kvpType);
            var createEnumerator = BuildCreateEnumeratorDelegate(enumeratorType, enumeratorCtor, diagNodeType);

            return new BuildResult(createNode, createEnumerator, enumeratorType);
        }

        private static CreateNodeDelegate BuildCreateNodeDelegate(Type diagNodeType, ConstructorInfo nodeCtor, FieldInfo nextField, Type kvpType)
        {
            // object CreateNode(KeyValuePair<string, object?> value, object? next)
            // {
            //     var node = new DiagNode<KeyValuePair<string, object?>>(value);
            //     node.Next = (DiagNode<KeyValuePair<string, object?>>?)next;
            //     return node;
            // }
            var dm = new DynamicMethod(
                "CreateDiagNode",
                returnType: typeof(object),
                parameterTypes: new[] { kvpType, typeof(object) },
                typeof(ActivityTagStorageEmitter<TTarget>).Module,
                skipVisibility: true);

            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Newobj, nodeCtor);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Castclass, diagNodeType);
            il.Emit(OpCodes.Stfld, nextField);
            il.Emit(OpCodes.Ret);

            return (CreateNodeDelegate)dm.CreateDelegate(typeof(CreateNodeDelegate));
        }

        private static CreateEnumeratorDelegate BuildCreateEnumeratorDelegate(Type enumeratorType, ConstructorInfo enumeratorCtor, Type diagNodeType)
        {
            // object CreateEnumerator(object? headNode)
            // {
            //     return (object)new Activity.Enumerator<KeyValuePair<string, object?>>((DiagNode<KeyValuePair<string, object?>>?)headNode);
            // }
            var dm = new DynamicMethod(
                "CreateEnumerator",
                returnType: typeof(object),
                parameterTypes: new[] { typeof(object) },
                typeof(ActivityTagStorageEmitter<TTarget>).Module,
                skipVisibility: true);

            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, diagNodeType);
            il.Emit(OpCodes.Newobj, enumeratorCtor);
            il.Emit(OpCodes.Box, enumeratorType);
            il.Emit(OpCodes.Ret);

            return (CreateEnumeratorDelegate)dm.CreateDelegate(typeof(CreateEnumeratorDelegate));
        }

        // Not a named-element ValueTuple: that requires TupleElementNamesAttribute, which isn't available
        // when compiling this project for net461 (see AGENTS.md - no ValueTuple syntax for .NET Framework 4.6.1).
        private readonly struct BuildResult
        {
            public BuildResult(CreateNodeDelegate? createNode, CreateEnumeratorDelegate? createEnumerator, Type? enumeratorType)
            {
                CreateNode = createNode;
                CreateEnumerator = createEnumerator;
                EnumeratorType = enumeratorType;
            }

            public CreateNodeDelegate? CreateNode { get; }

            public CreateEnumeratorDelegate? CreateEnumerator { get; }

            public Type? EnumeratorType { get; }
        }
    }
}
