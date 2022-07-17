// <copyright file="AsyncHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Instrumentation;

namespace Datadog.Trace.Debugger.Helpers
{
    internal static class AsyncHelper
    {
        private const string StateMachineNamePrefix = "<";
        private const string StateMachineNameSuffix = ">";
        private const string StateMachineFieldsPrefix = "<>";
        private const string BuilderFieldName = StateMachineFieldsPrefix + "t__builder";
        private const string StateFieldName = StateMachineFieldsPrefix + "1__state";
        private const string StateMachineThisFieldName = StateMachineFieldsPrefix + "4__this";
        private const string StateMachineLiveDebuggerAddedField = "<>dd_liveDebugger_isReEntryToMoveNext";
        private const BindingFlags AllFieldsBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static FieldNameValueType[] GetHoistedArgumentsFromStateMachine<TTarget>(TTarget instance, ParameterInfo[] originalMethodParameters)
        {
            if (originalMethodParameters is null || originalMethodParameters.Length == 0)
            {
                return Array.Empty<FieldNameValueType>();
            }

            var foundFields = new FieldNameValueType[originalMethodParameters.Length];
            var allFields = instance.GetType().GetFields(AllFieldsBindingFlags);

            int found = 0;
            for (int i = 0; i < allFields.Length; i++)
            {
                // we have more fields than parameters, so we break once we found all parameters
                if (found == foundFields.Length)
                {
                    break;
                }

                var field = allFields[i];
                // early exit here based on generated name pattern https://datadoghq.atlassian.net/browse/DEBUG-928
                if (field.Name.StartsWith(StateMachineNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                for (var j = 0; j < originalMethodParameters.Length; j++)
                {
                    if (field.Name == originalMethodParameters[j].Name)
                    {
                        foundFields[j] = new FieldNameValueType(field.Name, field.GetValue(instance), field.FieldType);
                        found++;
                        break;
                    }
                }
            }

            return foundFields;
        }

        /// <summary>
        /// MethodMetadataInfo saves locals from MoveNext localVarSig,
        /// this isn't enough in async scenario because we need to extract more locals the may hoisted in the builder object
        /// and we need to subtract some locals that exist in the localVarSig but they are not belongs to the kickoff method
        /// For know we capturing here all locals the are hoisted (except known generated locals)
        /// and we capturing in LogLocal the locals form localVarSig
        /// </summary>
        /// <typeparam name="TTarget">Instance type</typeparam>
        /// <param name="instance">Instance object</param>
        /// <param name="asyncKickoffInfo">The async kickoff info</param>
        /// <returns>List of locals</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static FieldInfoNameSanitized[] GetHoistedLocalsFromStateMachine<TTarget>(TTarget instance, AsyncKickoffMethodInfo asyncKickoffInfo)
        {
            var foundFields = new List<FieldInfoNameSanitized>();
            var allFields = instance.GetType().GetFields(AllFieldsBindingFlags);
            var kickoffParameters = asyncKickoffInfo.KickoffMethod.GetParameters();
            for (var i = 0; i < allFields.Length; i++)
            {
                var field = allFields[i];
                if (IsStateMachineField(field))
                {
                    continue;
                }

                bool foundInArguments = false;
                for (int j = 0; j < kickoffParameters.Length; j++)
                {
                    if (kickoffParameters[j].Name == field.Name)
                    {
                        foundInArguments = true;
                        break;
                    }
                }

                if (foundInArguments == false)
                {
                    var indexOfStateMachineSuffix = field.Name.IndexOf(StateMachineNameSuffix, StringComparison.Ordinal);
                    if (indexOfStateMachineSuffix > 0)
                    {
                        foundFields.Add(new FieldInfoNameSanitized(field, SanitizeAsyncHoistedLocalName(field.Name, indexOfStateMachineSuffix)));
                    }
                    else
                    {
                        foundFields.Add(new FieldInfoNameSanitized(field, field.Name));
                    }
                }
            }

            return foundFields.ToArray();
        }

        private static string SanitizeAsyncHoistedLocalName(string fieldName, int indexOfStateMachineSuffix)
        {
#if NETCOREAPP3_0_OR_GREATER
            return fieldName.AsSpan()[1..indexOfStateMachineSuffix].ToString();
#else
            return fieldName.Substring(1, indexOfStateMachineSuffix - 1);
#endif
        }

        private static bool IsStateMachineField(FieldInfo field)
        {
            if (field.Name.StartsWith(StateMachineFieldsPrefix))
            {
                AsyncMethodDebuggerInvoker.Log.Information($"Ignoring local (state machine field):  {field.FieldType.Name} {field.DeclaringType?.Name}.{field.Name}");
                return true;
            }

            return false;
        }

        private static bool IsKnownStateMachineField(FieldInfo field)
        {
            // builder (various object types), state (int) and "this"
            if (field.Name is BuilderFieldName or StateFieldName or StateMachineThisFieldName or StateMachineLiveDebuggerAddedField)
            {
                return true;
            }

            // awaiter etc. (e.g. <>u__1)
            if (field.Name.StartsWith(StateMachineFieldsPrefix))
            {
                if (field.FieldType.Name is "TaskAwaiter" or "TaskAwaiter`1" or "ValueTaskAwaiter" or "ValueTaskAwaiter`1")
                {
                    return true;
                }
                else
                {
                    AsyncMethodDebuggerInvoker.Log.Information($"Ignoring local {field.FieldType.Name} {field.DeclaringType?.Name}.{field.Name}");
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodBase GetAsyncKickoffMethod(Type stateMachineType)
        {
            var originalType = stateMachineType.DeclaringType;
            var allMethods = originalType?.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            MethodBase foundMethod = null;
            for (int i = 0; i < allMethods?.Length; i++)
            {
                var currentMethod = allMethods[i];
                if (currentMethod.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType == stateMachineType)
                {
                    foundMethod = currentMethod;
                    break;
                }
            }

            return foundMethod;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static object GetAsyncKickoffThisObject<TTarget>(TTarget stateMachine)
        {
            var thisField = stateMachine.GetType().GetField(StateMachineThisFieldName, BindingFlags.Instance | BindingFlags.Public);
            return thisField?.GetValue(stateMachine);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AsyncKickoffMethodInfo GetAsyncKickoffMethodInfo<TTarget>(TTarget stateMachine)
        {
            var parent = GetAsyncKickoffThisObject(stateMachine);
            var kickoffMethod = GetAsyncKickoffMethod(stateMachine.GetType());
            var kickoffParentType = parent?.GetType() ?? kickoffMethod.DeclaringType;
            return new AsyncKickoffMethodInfo(parent, kickoffParentType, kickoffMethod);
        }

        internal readonly ref struct AsyncKickoffMethodInfo
        {
            public AsyncKickoffMethodInfo(object kickoffParentObject, Type kickoffParentType, MethodBase kickoffMethod)
            {
                KickoffParentObject = kickoffParentObject;
                KickoffParentType = kickoffParentType;
                KickoffMethod = kickoffMethod;
            }

            public object KickoffParentObject { get; }

            public Type KickoffParentType { get; }

            public MethodBase KickoffMethod { get; }
        }

        // can't use ref struct here because GetHoistedArgumentsFromStateMachine returns FieldNameValue[]
        internal readonly record struct FieldNameValueType(string Name, object Value, Type Type)
        {
            public object Value { get; } = Value;

            public string Name { get; } = Name;

            public Type Type { get; } = Type;
        }

        internal readonly record struct FieldInfoNameSanitized(FieldInfo Field, string SanitizedName)
        {
            public FieldInfo Field { get; } = Field;

            public string SanitizedName { get; } = SanitizedName;
        }
    }
}
