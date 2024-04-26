// <copyright file="ShadowStackTree.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ShadowStackTree
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ShadowStackTree));

        private readonly AsyncLocal<TrackedStackFrameNode?> _trackedStackFrameActiveNode = new();
        private ReaderWriterLockSlim _lock;
        private HashSet<int> _uniqueSequencesLeaves;
        private TrackedStackFrameNode? _trackedStackFrameRootNode;
        private ConcurrentDictionary<TrackedStackFrameNode, Exception> _prematurelyUnwoundedFrames;

        internal ShadowStackTree()
        {
            _uniqueSequencesLeaves = new HashSet<int>();
            _prematurelyUnwoundedFrames = new ConcurrentDictionary<TrackedStackFrameNode, Exception>();
            _lock = new ReaderWriterLockSlim();
        }

        public TrackedStackFrameNode? CurrentStackFrameNode => _trackedStackFrameActiveNode.Value;

        public bool IsInRequestContext { get; set; }

        public TrackedStackFrameNode Enter(MethodBase method, bool isInvalidPath = false)
        {
            _trackedStackFrameActiveNode.Value = new TrackedStackFrameNode(_trackedStackFrameActiveNode.Value, method, isInvalidPath);
            return _trackedStackFrameActiveNode.Value;
        }

        public bool Leave(TrackedStackFrameNode trackedStackFrameNode, Exception? exception)
        {
            var currentActiveNode = _trackedStackFrameActiveNode.Value;

            // If the top of the shadow stack is different from the unwinding frame, we consider it to be 'prematurely unwounded' and keep it for future unwindings.
            if (trackedStackFrameNode != currentActiveNode)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Leave: The top of the shadowstack is different than the unwinding method, keeping it aside for future unwindings");
                }

                if (exception != null)
                {
                    _prematurelyUnwoundedFrames.TryAdd(trackedStackFrameNode, exception);
                }
                else
                {
                    Log.Debug("Leave: exception object is null.");
                }

                return false;
            }

            var parent = currentActiveNode!.RecordFunctionExit(exception);

            if (parent == null && exception != null)
            {
                _trackedStackFrameRootNode = currentActiveNode;
            }

            _trackedStackFrameActiveNode.Value = parent;

            // Try to handle previously collected prematurely unwounded frames.
            if (!_prematurelyUnwoundedFrames.IsEmpty &&
                _trackedStackFrameActiveNode.Value != null &&
                _prematurelyUnwoundedFrames.TryRemove(_trackedStackFrameActiveNode.Value, out var prematureException))
            {
                Leave(_trackedStackFrameActiveNode.Value, prematureException);
            }

            return _trackedStackFrameRootNode == currentActiveNode;
        }

        public void Leave(TrackedStackFrameNode trackedStackFrameNode)
        {
            Leave(trackedStackFrameNode, exception: null);
        }

        public ExceptionStackTreeRecord CreateResultReport(Exception exceptionPath, int stackSize = int.MaxValue)
        {
            var tree = new ExceptionStackTreeRecord();

            if (_trackedStackFrameRootNode == null && _trackedStackFrameActiveNode.Value == null)
            {
                Log.Warning($"{nameof(ShadowStackTree)}: returning an empty tree.");
                return tree;
            }

            var rootNode = _trackedStackFrameRootNode ?? _trackedStackFrameActiveNode.Value;

            if (rootNode == null)
            {
                return tree;
            }

            if (!rootNode.HasChildException(exceptionPath))
            {
                Log.Warning("The root node has a different exception. Root Node Exception: {RootNodeException}, Reported Exception: {ReportedException}", rootNode.LeavingException?.ToString(), exceptionPath?.ToString());
                return tree;
            }

            var knownNodes = new Stack<Tuple<int, TrackedStackFrameNode>>();
            knownNodes.Push(new Tuple<int, TrackedStackFrameNode>(0, rootNode));

            while (knownNodes.Count > 0 && tree.Frames.Count < stackSize)
            {
                var node = knownNodes.Pop();
                var level = node.Item1;
                var trackedStackFrame = node.Item2;

                if (trackedStackFrame.IsDisposed)
                {
                    throw new InvalidOperationException("Attempting to generate result report from a TrackedStackFrameNode which has already been disposed.");
                }

                if (trackedStackFrame.CapturingStrategy != SnapshotCapturingStrategy.None)
                {
                    tree.Add(level, trackedStackFrame);
                }

                if (trackedStackFrame.ChildNodes == null || !trackedStackFrame.ChildNodes.Any())
                {
                    break;
                }

                // TODO multiple children in AggregateException?
                knownNodes.Push(new Tuple<int, TrackedStackFrameNode>(level + 1, trackedStackFrame.ChildNodes.First()));
            }

            return tree;
        }

        public bool ContainsReport(Exception exceptionPath)
        {
            if (_trackedStackFrameRootNode == null && _trackedStackFrameActiveNode.Value == null)
            {
                return false;
            }

            var rootNode = _trackedStackFrameRootNode ?? _trackedStackFrameActiveNode.Value;

            return rootNode?.HasChildException(exceptionPath) == true;
        }

        public bool AddUniqueId(int id)
        {
            _lock.EnterWriteLock();
            try
            {
                return _uniqueSequencesLeaves.Add(id);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool ContainsUniqueId(int id)
        {
            _lock.EnterReadLock();
            try
            {
                return _uniqueSequencesLeaves.Contains(id);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool RemoveUniqueId(int id)
        {
            _lock.EnterWriteLock();
            try
            {
                return _uniqueSequencesLeaves.Remove(id);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _trackedStackFrameRootNode?.Dispose();
            _trackedStackFrameRootNode = null;
            _trackedStackFrameActiveNode.Value?.Dispose();
            _trackedStackFrameActiveNode.Value = null;
            IsInRequestContext = false;
            _uniqueSequencesLeaves.Clear();
            _prematurelyUnwoundedFrames.Clear();
            _lock.Dispose();
        }

        public void Init()
        {
            _uniqueSequencesLeaves = new HashSet<int>();
            _prematurelyUnwoundedFrames = new ConcurrentDictionary<TrackedStackFrameNode, Exception>();
            _lock = new ReaderWriterLockSlim();
        }
    }
}
