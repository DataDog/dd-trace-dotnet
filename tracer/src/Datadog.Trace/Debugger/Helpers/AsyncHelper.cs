// <copyright file="AsyncHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
        private const BindingFlags AllFieldsBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static FieldNameValue[] GetHoistedArgumentsFromStateMachine<TTarget>(TTarget instance, ParameterInfo[] originalMethodParameters)
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
                // early exit here based on generated name pattern https://datadoghq.atlassian.net/browse/DEBUG-928
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

        /// <summary>
        /// MethodMetadataInfo saves locals from MoveNext localVarSig,
        /// this isn't enough in async scenario because we need to extract more locals the may hoisted in the builder object
        /// and we need to subtract some locals that exist in the localVarSig but they are not belongs to the kickoff method
        /// For know we capturing here all locals the are hoisted (except known generated locals)
        /// and we capturing in LogLocal the locals form localVarSig
        /// </summary>
        /// <typeparam name="TTarget">Instance type</typeparam>
        /// <param name="instance">Instance object</param>
        /// <returns>List of locals</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static List<FieldNameValue> GetHoistedLocalsFromStateMachine<TTarget>(TTarget instance)
        {
            var foundFields = new List<FieldNameValue>();
            var allFields = instance.GetType().GetFields(AllFieldsBindingFlags);
            for (int i = 0; i < allFields.Length; i++)
            {
                var field = allFields[i];
                if (field.Name is BuilderFieldName or StateFieldName or StateMachineThisFieldName)
                {
                    continue;
                }

                if (field.Name.Contains(StateMachineNameSuffix))
                {
                    foundFields.Add(new FieldNameValue(field.Name, field.GetValue(instance)));
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
