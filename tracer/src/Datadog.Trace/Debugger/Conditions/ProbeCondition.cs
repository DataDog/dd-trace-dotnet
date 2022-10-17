// <copyright file="ProbeCondition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Conditions
{
    /// <summary>
    /// Method phase
    /// </summary>
    public enum MethodPhase
    {
        /// <summary>
        /// Entry of the method
        /// </summary>
        Entry,

        /// <summary>
        /// End of the method
        /// </summary>
        End
    }

    internal class ProbeCondition
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeCondition));
        private readonly List<ScopeMember> _scopeMembers;

        public ProbeCondition(string conditionId, MethodPhase evaluateAt, string conditionJson)
        {
            Id = conditionId;
            EvaluateAt = evaluateAt;
            ConditionJson = conditionJson;
            _scopeMembers = new List<ScopeMember>();
            Condition = new Lazy<Condition>(() => ProbeConditionExpressionParser.ToCondition(ConditionJson, InvocationTarget, ScopeMembers));
        }

        public Lazy<Condition> Condition { get; set; }

        public bool ShouldEvaluate { get; set; }

        public string Id { get; }

        // we can save this as T but it will require native changes to return MethodDebuggerState<T> instead MethodDebuggerState
        public ScopeMember InvocationTarget { get; private set; }

        public MethodPhase EvaluateAt { get; }

        public string ConditionJson { get; }

        public ReadOnlyCollection<ScopeMember> ScopeMembers => _scopeMembers.AsReadOnly();

        public bool IsEntryMethodCondition
        {
            get
            {
                return EvaluateAt == MethodPhase.Entry;
            }
        }

        public bool IsEndMethodCondition
        {
            get
            {
                return !IsEntryMethodCondition;
            }
        }

        private void AddMember(string name, Type type, object value, ScopeMember.MemberType eleMemberType)
        {
            if (!Enum.IsDefined(typeof(ScopeMember.MemberType), eleMemberType))
            {
                throw new ArgumentException(nameof(eleMemberType) + "is not a valid enum value");
            }

            _scopeMembers.Add(new ScopeMember(name, type, value, eleMemberType));
        }

        public void AddArgument(string name, Type type, object value)
        {
            AddMember(name, type, value, ScopeMember.MemberType.Argument);
        }

        public void AddLocal(string name, Type type, object value)
        {
            AddMember(name, type, value, ScopeMember.MemberType.Local);
        }

        public void AddException(string name, Type type, object value)
        {
            AddMember(name, type, value, ScopeMember.MemberType.Exception);
        }

        public void AddReturn(string name, Type type, object value)
        {
            AddMember(name, type, value, ScopeMember.MemberType.Return);
        }

        public void SetInvocationTarget(string name, Type type, object value)
        {
            InvocationTarget = new ScopeMember(name, type, value, ScopeMember.MemberType.This);
        }

        public bool Evaluate()
        {
            if (InvocationTarget.Type == null || string.IsNullOrEmpty(ConditionJson))
            {
                throw new NullReferenceException("Either invocation target is null or condition json file is null or empty");
            }

            ShouldEvaluate = false;
            try
            {
                return Condition.Value.Predicate(InvocationTarget, ScopeMembers);
            }
            catch (Exception e)
            {
                Log.Error(e, $"Failed to run condition for probe {Id}. Condition was {ConditionJson}");
                return true;
            }
        }

        public void SetShouldEvaluate(MethodPhase methodPhase)
        {
            ShouldEvaluate = methodPhase == EvaluateAt;
        }
    }

    internal record Condition
    {
        public Func<ScopeMember, ReadOnlyCollection<ScopeMember>, bool> Predicate { get; set; }

        public string DSL { get; set; }

        public Expression Expression { get; set; }
    }
}
