// <copyright file="CallTargetState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.CallTarget
{
    /// <summary>
    /// Call target execution state
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct CallTargetState
    {
        private CallTargetStateInternal _item;

        /// <summary>
        /// Initializes a new instance of the <see cref="CallTargetState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        internal CallTargetState(Scope scope)
        {
            var item = ObjectPool<CallTargetStateInternal>.Shared.Get();
            item.PreviousScope = null;
            item.Scope = scope;
            item.State = null;
            item.StartTime = null;
            item.PreviousDistributedSpanContext = null;
            _item = item;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CallTargetState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        /// <param name="state">Object state instance</param>
        internal CallTargetState(Scope scope, object state)
        {
            var item = ObjectPool<CallTargetStateInternal>.Shared.Get();
            item.PreviousScope = null;
            item.Scope = scope;
            item.State = state;
            item.StartTime = null;
            item.PreviousDistributedSpanContext = null;
            _item = item;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CallTargetState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        /// <param name="state">Object state instance</param>
        /// <param name="startTime">The intended start time of the scope, intended for scopes created in the OnMethodEnd handler</param>
        internal CallTargetState(Scope scope, object state, DateTimeOffset? startTime)
        {
            var item = ObjectPool<CallTargetStateInternal>.Shared.Get();
            item.PreviousScope = null;
            item.Scope = scope;
            item.State = state;
            item.StartTime = startTime;
            item.PreviousDistributedSpanContext = null;
            _item = item;
        }

        internal CallTargetState(Scope previousScope, IReadOnlyDictionary<string, string> previousDistributedSpanContext, CallTargetState state)
        {
            var item = ObjectPool<CallTargetStateInternal>.Shared.Get();
            item.PreviousScope = previousScope;
            item.Scope = state._item.Scope;
            item.State = state._item.State;
            item.StartTime = state._item.StartTime;
            item.PreviousDistributedSpanContext = previousDistributedSpanContext;
            _item = item;
        }

        /// <summary>
        /// Gets the CallTarget BeginMethod scope
        /// </summary>
        internal Scope Scope => _item.Scope;

        /// <summary>
        /// Gets the CallTarget BeginMethod state
        /// </summary>
        public object State => _item.State;

        /// <summary>
        /// Gets the CallTarget state StartTime
        /// </summary>
        public DateTimeOffset? StartTime => _item.StartTime;

        internal Scope PreviousScope => _item.PreviousScope;

        internal IReadOnlyDictionary<string, string> PreviousDistributedSpanContext => _item.PreviousDistributedSpanContext;

        /// <summary>
        /// Gets the default call target state (used by the native side to initialize the locals)
        /// </summary>
        /// <returns>Default call target state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetState GetDefault()
        {
            return default;
        }

        /// <summary>
        /// ToString override
        /// </summary>
        /// <returns>String value</returns>
        public override string ToString()
        {
            return $"{typeof(CallTargetState).FullName}({_item?.PreviousScope}, {_item?.Scope}, {_item?.State})";
        }

        /// <summary>
        /// Release internal data
        /// </summary>
        public void Release()
        {
            var item = _item;
            _item = null;
            item.PreviousScope = null;
            item.Scope = null;
            item.State = null;
            item.StartTime = null;
            item.PreviousDistributedSpanContext = null;
            ObjectPool<CallTargetStateInternal>.Shared.Return(item);
        }
    }

#pragma warning disable CS0649, SA1401
    internal class CallTargetStateInternal
    {
        public Scope PreviousScope;
        public Scope Scope;
        public object State;
        public DateTimeOffset? StartTime;
        public IReadOnlyDictionary<string, string> PreviousDistributedSpanContext;
    }
}
