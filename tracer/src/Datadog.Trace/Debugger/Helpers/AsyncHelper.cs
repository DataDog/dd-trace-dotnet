// <copyright file="AsyncHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.Helpers
{
    internal static class AsyncHelper
    {
        private const string StateMachineNamePrefix = "Gen"; // <
        private const string StateMachineNameSuffix = "End"; // >
        private const string StateMachineFieldsPrefix = "GenEnd"; // "<>";
        private const string BuilderFieldName = StateMachineFieldsPrefix + "t__builder"; // "<>t__builder";
        private const string StateFieldName = StateMachineFieldsPrefix + "1__state"; // "<>t__builder";
        private const string StateMachineThisFieldName = StateMachineFieldsPrefix + "4__this";
        private const string BuilderTaskFieldName = "m_task";
        private const BindingFlags AllFieldsBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static FieldInfo GetStateMachineBuilderField(object stateMachine) => stateMachine?.GetType().GetField(BuilderFieldName);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static object GetStateMachineBuilderValue(object stateMachine) => GetStateMachineBuilderField(stateMachine).GetValue(stateMachine);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Task GetBuilderTaskValue(object taskBuilder) => (Task)taskBuilder?.GetType().GetField(BuilderTaskFieldName)?.GetValue(taskBuilder);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Task GetStateMachineBuilderTaskValue(object stateMachine) => GetBuilderTaskValue(GetStateMachineBuilderValue(stateMachine));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // if 1__state not found, it's better to throw here
        internal static int GetStateMachineStateValue(object stateMachine) => (int)stateMachine?.GetType().GetField(StateFieldName)?.GetValue(stateMachine);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static object GetKickoffThisObjectFromStateMachine<TTarget>(TTarget stateMachine)
        {
            FieldInfo thisField = stateMachine.GetType().GetField(StateMachineThisFieldName, BindingFlags.Instance | BindingFlags.Public);
            if (thisField == null)
            {
                if (GetAsyncKickoffMethod(stateMachine.GetType()).IsStatic)
                {
                    return null;
                }
                else
                {
                    // hoisted "this" has not found
                    // TODO: ???
                    return null;
                }
            }

            return thisField.GetValue(stateMachine);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static FieldNameValue[] GetKickOffMethodArgumentsFromStateMachine<TTarget>(TTarget instance, ParameterInfo[] originalMethodParameters)
        {
            if (originalMethodParameters is null || originalMethodParameters.Length == 0)
            {
                return null;
            }

            var foundFields = new FieldNameValue[originalMethodParameters.Length];
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
                // early exit here based on generated name pattern
                if (field.Name.StartsWith(StateMachineNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                for (int j = 0; j < originalMethodParameters.Length; j++)
                {
                    if (field.Name == originalMethodParameters[j].Name)
                    {
                        foundFields[j] = new FieldNameValue(field.Name, field.GetValue(instance));
                        found++;
                        break;
                    }
                }
            }

            return foundFields;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static FieldNameValue[] GetKickOffMethodLocalsFromStateMachine<TTarget>(TTarget instance, Type stateMachineType)
        {
            var kickOffMethod = GetAsyncKickoffMethod(stateMachineType);
            var originalLocals = kickOffMethod.GetMethodBody()?.LocalVariables;
            if (originalLocals is null || originalLocals.Count == 0)
            {
                return null;
            }

            var foundFields = new FieldNameValue[originalLocals.Count];
            var allFields = instance.GetType().GetFields(AllFieldsBindingFlags);
            int found = 0;
            for (int i = 0; i < allFields.Length; i++)
            {
                // we have more fields than locals, so we break once we found all parameters
                if (found == foundFields.Length)
                {
                    break;
                }

                var field = allFields[i];
                if (field.Name.Contains(StateMachineNameSuffix))
                {
                    foundFields[found] = new FieldNameValue(field.Name, field.GetValue(instance));
                    found++;
                }
            }

            return foundFields;
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
                    // theoretically we can heave more than one found, we can iterate to the end to verify it
                    foundMethod = currentMethod;
                    break;
                }
            }

            return foundMethod;
        }

        internal readonly record struct FieldNameValue(string Name, object Value);
    }
}
