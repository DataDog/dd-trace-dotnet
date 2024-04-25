// <copyright file="AsyncHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Instrumentation;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Helpers
{
    internal static class AsyncHelper
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AsyncHelper));

        private const string StateMachineNamePrefix = "<";
        private const string StateMachineNameSuffix = ">";
        private const string StateMachineFieldsNamePrefix = "<>";
        private const string ThisName = "__this";
        private const string StateMachineThisFieldName = StateMachineFieldsNamePrefix + "4" + ThisName;
        // Should be the same as in debugger_tokens.h managed_profiler_debugger_is_first_entry_field_name
        private const string StateMachineLiveDebuggerAddedField = "<>dd_liveDebugger_isReEntryToMoveNext";
        private const BindingFlags AllFieldsBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static FieldInfo[] GetHoistedArgumentsFromStateMachine(Type stateMachineType, string[] originalMethodParametersName)
        {
            if (originalMethodParametersName is null || originalMethodParametersName.Length == 0)
            {
                return Array.Empty<FieldInfo>();
            }

            var foundFields = new FieldInfo[originalMethodParametersName.Length];
            var allFields = stateMachineType.GetFields(AllFieldsBindingFlags);

            int found = 0;
            for (int i = 0; i < allFields.Length; i++)
            {
                // we have more fields than parameters, so we break once we found all parameters
                if (found == foundFields.Length)
                {
                    break;
                }

                var field = allFields[i];
                // If we can just guess the name based on the C# compiler generated name pattern, then we're done
                if (field.Name.StartsWith(StateMachineNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                for (var j = 0; j < originalMethodParametersName.Length; j++)
                {
                    if (field.Name == originalMethodParametersName[j])
                    {
                        foundFields[j] = field;
                        found++;
                        break;
                    }
                }
            }

            return foundFields;
        }

        /// <summary>
        /// MethodMetadataInfo stores the locals that appear in a method's localVarSig,
        /// This isn't enough in the async scenario (for "MoveNext") because we need to extract more locals that may have been hoisted in the builder object,
        /// and we need to subtract some locals that exist in the localVarSig but they do not belongs to the kickoff method (compiler generated locals for the MoveNext method usage).
        /// For now, we're capturing all locals that are hoisted (except known generated locals),
        /// and we're capturing all the locals that appear in localVarSig with `LogLocal`
        /// </summary>
        /// <param name="stateMachineType">The <see cref="System.Type"/> of the state machine</param>
        /// <param name="asyncKickoffInfo">The async kickoff info</param>
        /// <returns>List of locals</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static FieldInfoNameSanitized[] GetHoistedLocalsFromStateMachine(Type stateMachineType, AsyncKickoffMethodInfo asyncKickoffInfo)
        {
            var foundFields = new List<FieldInfoNameSanitized>();
            var allFields = stateMachineType.GetFields(AllFieldsBindingFlags);
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
            return fieldName.Substring(1, indexOfStateMachineSuffix - 1);
        }

        private static bool IsStateMachineField(FieldInfo field)
        {
            if (field.Name.StartsWith(StateMachineFieldsNamePrefix))
            {
                Log.Debug("Ignoring local (state machine field): {FieldType} {DeclaringType} {FieldName}", field.FieldType.Name, field.DeclaringType?.Name, field.Name);
                return true;
            }

            return false;
        }

        private static bool IsKnownStateMachineField(FieldInfo field)
        {
            const string builderFieldName = StateMachineFieldsNamePrefix + "t__builder";
            const string stateFieldName = StateMachineFieldsNamePrefix + "1__state";

            // builder (various object types), state (int) and "this"
            if (field.Name is builderFieldName or stateFieldName or StateMachineThisFieldName or StateMachineLiveDebuggerAddedField)
            {
                return true;
            }

            // awaiter etc. (e.g. <>u__1)
            if (field.Name.StartsWith(StateMachineFieldsNamePrefix))
            {
                if (field.FieldType.Name is "TaskAwaiter" or "TaskAwaiter`1" or "ValueTaskAwaiter" or "ValueTaskAwaiter`1")
                {
                    return true;
                }
                else
                {
                    AsyncMethodDebuggerInvoker.Log.Information("Ignoring local {FieldType} {DeclaringType}.{FieldName}", field.FieldType.Name, field.DeclaringType?.Name, field.Name);
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
                if (currentMethod.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType ==
                    (stateMachineType.IsGenericType ? stateMachineType.GetGenericTypeDefinition() : stateMachineType))
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
            if (stateMachine == null)
            {
                // State machine is null in case is a nested struct inside a generic parent.
                // This can happen if we operate in optimized code and the original async method was inside a generic class
                // or in case the original async method was generic, in which case the state machine is a generic value type
                // See more here: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Tracer.Native/method_rewriter.cpp#L70
                return null;
            }

            var thisField = stateMachine.GetType().GetField(StateMachineThisFieldName, BindingFlags.Instance | BindingFlags.Public);
            if (thisField == null)
            {
                var allFields = stateMachine.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
                for (int i = 0; i < allFields.Length; i++)
                {
                    var field = allFields[i];
                    if (char.IsNumber(field.Name[2]) && field.Name.StartsWith(StateMachineFieldsNamePrefix) && field.Name.EndsWith(ThisName))
                    {
                        thisField = field;
                        break;
                    }
                }
            }

            return thisField?.GetValue(stateMachine);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AsyncKickoffMethodInfo GetAsyncKickoffMethodInfo<TTarget>(TTarget stateMachine, Type stateMachineType)
        {
            // tries to grab the hoisted 'this' if one exists, returns null when we fail to do so
            var kickoffParentObject = GetAsyncKickoffThisObject(stateMachine);
            var kickoffMethod = GetAsyncKickoffMethod(stateMachineType);
            var kickoffParentType = kickoffParentObject?.GetType() ?? kickoffMethod.DeclaringType;
            return new AsyncKickoffMethodInfo(kickoffParentObject, kickoffParentType, kickoffMethod);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAsync(MethodBase method)
        {
            return method?.DeclaringType?.GetInterfaces().Any(i => i == typeof(IAsyncStateMachine)) == true;
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

        // can't use ref struct here because GetHoistedArgumentsFromStateMachine returns FieldInfoNameSanitized[]
        internal readonly record struct FieldInfoNameSanitized(FieldInfo Field, string SanitizedName)
        {
            public FieldInfo Field { get; } = Field;

            public string SanitizedName { get; } = SanitizedName;
        }
    }
}
