// <copyright file="ILAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal static class ILAnalyzer
    {
        public static bool HasDirectCallTo(MethodBase method, Type targetType, string methodName)
        {
            try
            {
                var effectiveMethod = GetEffectiveMethod(method);
                return AnalyzeMethodBody(effectiveMethod, targetType, methodName);
            }
            catch
            {
                // If we can't analyze the method for any reason, assume it might not contain the call
                return false;
            }
        }

        private static MethodBase GetEffectiveMethod(MethodBase method)
        {
            // For async methods, analyze the MoveNext method
            if (method.GetCustomAttribute<AsyncStateMachineAttribute>() is AsyncStateMachineAttribute asyncAttribute)
            {
                var stateMachineType = asyncAttribute.StateMachineType;
                return stateMachineType.GetMethod(
                           "MoveNext",
                           BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ?? method;
            }

            return method;
        }

        private static bool AnalyzeMethodBody(MethodBase methodToAnalyze, Type targetType, string methodName)
        {
            var methodBody = methodToAnalyze.GetMethodBody();
            if (methodBody == null)
            {
                return false;
            }

            var il = methodBody.GetILAsByteArray();
            if (il == null)
            {
                return false;
            }

            int position = 0;
            while (position < il.Length)
            {
                // Read opcode
                int opcode = il[position++];
                if (opcode == 0xFE && position < il.Length)
                {
                    opcode = 0xFE00 | il[position++];
                }

                // Check for call instructions
                if (opcode == OpCodes.Call.Value || opcode == OpCodes.Callvirt.Value)
                {
                    // Ensure we have enough bytes for the token
                    if (position + 3 >= il.Length)
                    {
                        break;
                    }

                    int token = il[position] |
                               (il[position + 1] << 8) |
                               (il[position + 2] << 16) |
                               (il[position + 3] << 24);

                    position += 4;

                    try
                    {
                        var calledMethod = methodToAnalyze?.Module?.ResolveMethod(token);
                        if (calledMethod?.DeclaringType == targetType &&
                            calledMethod.Name == methodName)
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // If we can't resolve this specific method, continue checking others
                        continue;
                    }
                }
                else
                {
                    // Skip operands for other opcodes
                    position += GetOperandSize(opcode);
                }
            }

            return false;
        }

        private static int GetOperandSize(int opcode) => opcode switch
        {
            0x28 or 0x6F or 0x70 or 0x11 or 0x20 => 4, // Various 4-byte operand instructions
            0x73 or 0x6E => 4, // Newobj, Ldtoken
            0x1B or 0x31 or 0x15 or 0x2A => 1, // Various 1-byte operand instructions
            _ => 0
        };
    }
}
