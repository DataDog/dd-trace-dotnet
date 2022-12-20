// <copyright file="ProbeExpressionsProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Instrumentation;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Conditions
{
    internal class ProbeExpressionsProcessor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeExpressionsProcessor));

        private static object _globalInstanceLock = new();

        private static bool _globalInstanceInitialized;

        private static ProbeExpressionsProcessor _instance;

        private readonly ConcurrentDictionary<string, ProbeExpressionEvaluatorBase> _debuggerExpressions = new();

        internal static ProbeExpressionsProcessor Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _globalInstanceInitialized,
                    ref _globalInstanceLock);
            }
        }

        public ProbeExpressionEvaluatorBase Get(string probeId)
        {
            _debuggerExpressions.TryGetValue(probeId, out var probeExpressionEvaluator);
            return probeExpressionEvaluator;
        }

        internal bool HasExpression(string probeId)
        {
            return _debuggerExpressions.ContainsKey(probeId);
        }

        public void AddExpressions(string probeId, ProbeType probeType, EvaluateAt evaluateAt, DebuggerExpression[] debuggerExpressions)
        {
            try
            {
                _debuggerExpressions.TryAdd(probeId, ProbeExpressionEvaluatorBase.CreateEvaluator(probeId, probeType, evaluateAt, debuggerExpressions));
            }
            catch (Exception e)
            {
                Log.Error(e, $"Failed to add probe expressions for probe {probeId}");
            }
        }

        public void Remove(string probeId)
        {
            _debuggerExpressions.TryRemove(probeId, out _);
        }

        internal unsafe bool Process(ref MethodDebuggerState state, delegate* managed<ref MethodDebuggerState, void> finalizeSnapshotFuncPointer = null)
        {
            var evaluator = Get(state.ProbeId);
            if (evaluator == null)
            {
                // no expression for this probe, continue capturing and return
                return false;
            }

            if (!evaluator.ShouldEvaluate(state.MethodPhase))
            {
                // there is an expression but we are in the method start phase and the evaluation should be at method end
                return true;
            }

            // now do the evaluation
            evaluator.AddMember("this", state.MethodMetadataInfo.DeclaringType, state.InvocationTarget, ScopeMemberKind.This);
            var result = evaluator.Evaluate();
            if (!result.Succeeded)
            {
                // evaluation failure e.g. failed to dereference object
                // todo: capture the error
                return true;
            }

            // evaluation succeeded, act based on the probe type
            evaluator.Post(ref result, ref state, finalizeSnapshotFuncPointer);
            return true;
        }

        internal bool AddMemberIfNeeded<TArg>(ref MethodDebuggerState state, string name, Type memberType, TArg member, ScopeMemberKind memberKind)
        {
            if (!_debuggerExpressions.TryGetValue(state.ProbeId, out var evaluator))
            {
                return false;
            }

            if (!evaluator.ShouldEvaluate(state.MethodPhase))
            {
                return false;
            }

            evaluator.AddMember(name, memberType, member, memberKind);
            return true;
        }

        public bool HasExpression(string probeId, ref MethodDebuggerState state)
        {
            var evaluator = Get(probeId);
            evaluator?.CreateMethodScopeMembers(state.MethodMetadataInfo.LocalVariableNames.Length, state.MethodMetadataInfo.ParameterNames.Length);
            return evaluator != null;
        }
    }
}
