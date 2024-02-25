// <copyright file="TrackedStackFrameNode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.Snapshots;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class TrackedStackFrameNode
    {
        private TrackedStackFrameNode _parent;
        private List<TrackedStackFrameNode> _activeChildNodes;
        private bool _disposed;
        private uint? _enterSequenceHash;
        private uint? _leaveSequenceHash;
        private bool _childNodesAlreadyCleansed;
        private string _snapshot;
        private string _snapshotId;

        public TrackedStackFrameNode(TrackedStackFrameNode parent, MethodBase method, bool isInvalidPath = false)
        {
            _parent = parent;
            Method = method;
            IsInvalidPath = isInvalidPath;
        }

        public uint EnterSequenceHash
        {
            get
            {
                _enterSequenceHash ??= ComputeEnterSequenceHash();
                return _enterSequenceHash.Value;
            }
        }

        public uint LeaveSequenceHash
        {
            get
            {
                _leaveSequenceHash ??= ComputeLeaveSequenceHash();
                return _leaveSequenceHash.Value;
            }
        }

        public uint SequenceHash
        {
            get
            {
                return EnterSequenceHash ^ LeaveSequenceHash;
            }
        }

        public TrackedStackFrameNode Parent => _parent;

        public Exception LeavingException { get; private set; }

        public string Snapshot
        {
            get
            {
                _snapshot ??= CapturingStrategy == SnapshotCapturingStrategy.None ? string.Empty : CreateSnapshot();
                return _snapshot;
            }
        }

        public string SnapshotId
        {
            get
            {
                if (_snapshotId == null)
                {
                    _ = Snapshot;
                }

                return _snapshotId;
            }
        }

        public bool IsDisposed => _disposed;

        public bool IsInvalidPath { get; }

        public IEnumerable<TrackedStackFrameNode> ChildNodes => _activeChildNodes?.ToList() ?? Enumerable.Empty<TrackedStackFrameNode>();

        public MethodBase Method { get; }

        public bool IsFrameUnwound { get; private set; }

        internal string ProbeId { get; set; }

        internal MethodScopeMembers Members { get; set; }

        internal int MethodMetadataIndex { get; set; }

        internal bool IsAsyncMethod { get; set; }

        internal object MoveNextInvocationTarget { get; set; }

        internal object KickoffInvocationTarget { get; set; }

        internal bool? HasArgumentsOrLocals { get; set; }

        internal SnapshotCapturingStrategy CapturingStrategy { get; set; }

        internal int NumOfChildren { get; private set; }

        public void MarkAsUnwound()
        {
            IsFrameUnwound = true;
        }

        private string CreateSnapshot()
        {
            using var snapshotCreator = new DebuggerSnapshotCreator(isFullSnapshot: true, location: ProbeLocation.Method, hasCondition: false, Array.Empty<string>(), Members);

            _snapshotId = snapshotCreator.SnapshotId;

            ref var methodMetadataInfo = ref MethodMetadataCollection.Instance.Get(MethodMetadataIndex);

            CaptureInfo<object> info;

            if (IsAsyncMethod)
            {
                var asyncCaptureInfo = new AsyncCaptureInfo(MoveNextInvocationTarget, KickoffInvocationTarget, methodMetadataInfo.KickoffInvocationTargetType, methodMetadataInfo.KickoffMethod, methodMetadataInfo.AsyncMethodHoistedArguments, methodMetadataInfo.AsyncMethodHoistedLocals);
                info = new CaptureInfo<object>(MethodMetadataIndex, value: asyncCaptureInfo.KickoffInvocationTarget, type: asyncCaptureInfo.KickoffInvocationTargetType, methodState: MethodState.ExitEndAsync, memberKind: ScopeMemberKind.This, asyncCaptureInfo: asyncCaptureInfo, hasLocalOrArgument: HasArgumentsOrLocals);

                ProbeProcessor.AddAsyncMethodArguments(snapshotCreator, ref info);
                ProbeProcessor.AddAsyncMethodLocals(snapshotCreator, ref info);
            }
            else
            {
                info = new CaptureInfo<object>(MethodMetadataIndex, value: Members.InvocationTarget, type: methodMetadataInfo.DeclaringType, invocationTargetType: methodMetadataInfo.DeclaringType, memberKind: ScopeMemberKind.This, methodState: MethodState.ExitEnd, hasLocalOrArgument: HasArgumentsOrLocals, method: methodMetadataInfo.Method);
            }

            snapshotCreator.CaptureBehaviour = CaptureBehaviour.Evaluate;
            snapshotCreator.ProcessDelayedSnapshot(ref info, hasCondition: true);
            snapshotCreator.CaptureExitMethodEndMarker(ref info);
            return snapshotCreator.FinalizeMethodSnapshot(ProbeId, 1, ref info);
        }

        internal void AddScopeMember<T>(string name, Type type, T value, ScopeMemberKind memberKind)
        {
            if (Members == null)
            {
                Members = new MethodScopeMembers(0, 0);
            }

            type = (type.IsGenericTypeDefinition ? value?.GetType() : type) ?? type;
            switch (memberKind)
            {
                case ScopeMemberKind.This:
                    Members.InvocationTarget = new ScopeMember(name, type, value, ScopeMemberKind.This);
                    return;
                case ScopeMemberKind.Exception:
                    Members.Exception = value as Exception;
                    return;
                case ScopeMemberKind.Return:
                    Members.Return = new ScopeMember("return", type, value, ScopeMemberKind.Return);
                    return;
                case ScopeMemberKind.None:
                    return;
            }

            Members.AddMember(new ScopeMember(name, type, value, memberKind));
        }

        private IEnumerable<Exception> FlattenException(Exception exception)
        {
            var exceptionList = new Stack<Exception>();

            exceptionList.Push(exception);

            while (exceptionList.Count > 0)
            {
                var e = exceptionList.Pop();

                yield return e;

                if (e == null)
                {
                    continue;
                }

                if (e.InnerException != null && !(e is AggregateException))
                {
                    // Aggregate exceptions contains the InnerException in its InnerExceptions list
                    exceptionList.Push(e.InnerException);
                }

                if (e is AggregateException aggregateException)
                {
                    foreach (var aggExp in aggregateException.InnerExceptions)
                    {
                        exceptionList.Push(aggExp);
                    }
                }
            }
        }

        private uint ComputeEnterSequenceHash()
        {
            return Fnv1aHash.ComputeHash(_parent?.EnterSequenceHash ?? 0, Method.MetadataToken);
        }

        /// <summary>
        /// TODO take not only first child.
        /// </summary>
        private uint ComputeLeaveSequenceHash()
        {
            lock (this)
            {
                ClearNonRelevantChildNodes();

                if (_activeChildNodes?.Any() == true)
                {
                    var firstChild = _activeChildNodes.First();
                    return Fnv1aHash.ComputeHash(firstChild.LeaveSequenceHash, firstChild.Method.MetadataToken);
                }

                return 0;
            }
        }

        public TrackedStackFrameNode RecordFunctionExit(Exception ex)
        {
            if (ex == null)
            {
                return RecordFunctionExit();
            }

            LeavingException = ex;
            ClearNonRelevantChildNodes();

            if (_parent == null)
            {
                return null;
            }

            lock (_parent)
            {
                _parent._activeChildNodes ??= new List<TrackedStackFrameNode>();
                _parent._activeChildNodes.Add(this);
            }

            lock (this)
            {
                // TODO For AggregateException, first/default might no be the most suitable way to tackle it.
                var childCapturingCount = _activeChildNodes?.FirstOrDefault()?.NumOfChildren ?? 0;
                NumOfChildren = childCapturingCount + 1;
            }

            return _parent;
        }

        private TrackedStackFrameNode RecordFunctionExit()
        {
            var parent = _parent;
            Dispose();
            return parent;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _parent = null;
            if (_activeChildNodes != null)
            {
                foreach (var node in _activeChildNodes)
                {
                    node.Dispose();
                }
            }

            _activeChildNodes = null;
            MarkAsUnwound();
            _disposed = true;
        }

        private void ClearNonRelevantChildNodes()
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (_activeChildNodes == null || _childNodesAlreadyCleansed)
            {
                return;
            }

            lock (this)
            {
                if (_childNodesAlreadyCleansed)
                {
                    return;
                }

                if (!_activeChildNodes.Any())
                {
                    _activeChildNodes = null;
                    return;
                }

                for (var i = _activeChildNodes.Count - 1; i >= 0; i--)
                {
                    var frame = _activeChildNodes[i];
                    if (frame.LeavingException == null || !HasChildException(frame.LeavingException))
                    {
                        _activeChildNodes.RemoveAt(i);
                        frame.Dispose();
                    }
                }

                if (LeavingException != null)
                {
                    _childNodesAlreadyCleansed = true;
                }
            }
        }

        public bool HasChildException(Exception exception)
        {
            if (LeavingException == exception || LeavingException == exception?.InnerException)
            {
                return true;
            }

            var allActiveExceptions = FlattenException(exception);
            var allChildExceptions = FlattenException(LeavingException);

            return allActiveExceptions.Intersect(allChildExceptions).Any();
        }

        public override string ToString()
        {
            return $"{nameof(TrackedStackFrameNode)}(Child Count = {_activeChildNodes?.Count})";
        }
    }
}
