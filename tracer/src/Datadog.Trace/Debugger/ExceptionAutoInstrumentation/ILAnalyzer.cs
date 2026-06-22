// <copyright file="ILAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

#if NETCOREAPP
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
#else
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata.Ecma335;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.PortableExecutable;
#endif

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal static class ILAnalyzer
    {
        private static readonly OpCode[] SingleByteOpCodes = new OpCode[0x100];
        private static readonly OpCode[] MultiByteOpCodes = new OpCode[0x100];

        static ILAnalyzer()
        {
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is not OpCode opCode)
                {
                    continue;
                }

                var value = unchecked((ushort)opCode.Value);
                if (value < 0x100)
                {
                    SingleByteOpCodes[value] = opCode;
                }
                else if ((value & 0xFF00) == 0xFE00)
                {
                    MultiByteOpCodes[value & 0xFF] = opCode;
                }
            }
        }

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

            if (!TryCreateMetadataReader(methodToAnalyze.Module, out var stream, out var peReader, out var reader))
            {
                return false;
            }

            using (stream)
            using (peReader)
            {
                return AnalyzeMethodBody(il, reader, targetType, methodName);
            }
        }

        private static bool AnalyzeMethodBody(byte[] il, MetadataReader reader, Type targetType, string methodName)
        {
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
                    if (position + 3 >= il.Length)
                    {
                        break;
                    }

                    int token = il[position] |
                               (il[position + 1] << 8) |
                               (il[position + 2] << 16) |
                               (il[position + 3] << 24);

                    position += 4;

                    if (IsCallToTargetMethod(reader, token, targetType, methodName))
                    {
                        return true;
                    }
                }
                else
                {
                    // Skip operands for other opcodes
                    position += GetOperandSize(opcode, il, position);
                }
            }

            return false;
        }

        private static bool TryCreateMetadataReader(
            Module module,
            [NotNullWhen(true)] out FileStream? stream,
            [NotNullWhen(true)] out PEReader? peReader,
            [NotNullWhen(true)] out MetadataReader? reader)
        {
            stream = null;
            peReader = null;
            reader = default;

            if (string.IsNullOrEmpty(module.FullyQualifiedName) || !File.Exists(module.FullyQualifiedName))
            {
                return false;
            }

            try
            {
                stream = new FileStream(module.FullyQualifiedName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                peReader = new PEReader(stream, PEStreamOptions.PrefetchMetadata);
                reader = peReader.GetMetadataReader();
                return true;
            }
            catch
            {
                peReader?.Dispose();
                stream?.Dispose();
                peReader = null;
                stream = null;
                reader = default;
                return false;
            }
        }

        private static bool IsCallToTargetMethod(MetadataReader reader, int token, Type targetType, string methodName)
        {
            try
            {
                var handle = MetadataTokens.EntityHandle(token);
                return IsMethodHandleMatch(reader, handle, targetType, methodName);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsMethodHandleMatch(MetadataReader reader, EntityHandle handle, Type targetType, string methodName)
        {
            try
            {
                switch (handle.Kind)
                {
                    case HandleKind.MethodDefinition:
                        var methodDefinition = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                        return reader.GetString(methodDefinition.Name) == methodName &&
                               IsTargetType(reader, methodDefinition.GetDeclaringType(), targetType);

                    case HandleKind.MemberReference:
                        var memberReference = reader.GetMemberReference((MemberReferenceHandle)handle);
                        return reader.GetString(memberReference.Name) == methodName &&
                               IsTargetType(reader, memberReference.Parent, targetType);

                    case HandleKind.MethodSpecification:
                        var methodSpecification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                        return IsMethodHandleMatch(reader, methodSpecification.Method, targetType, methodName);
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool IsTargetType(MetadataReader reader, EntityHandle handle, Type targetType)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    var typeDefinition = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                    return TypeNameMatches(
                        reader.GetString(typeDefinition.Namespace),
                        reader.GetString(typeDefinition.Name),
                        targetType);

                case HandleKind.TypeReference:
                    var typeReference = reader.GetTypeReference((TypeReferenceHandle)handle);
                    return TypeNameMatches(
                        reader.GetString(typeReference.Namespace),
                        reader.GetString(typeReference.Name),
                        targetType);

                case HandleKind.MethodDefinition:
                    var methodDefinition = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    return IsTargetType(reader, methodDefinition.GetDeclaringType(), targetType);

                case HandleKind.MemberReference:
                    var memberReference = reader.GetMemberReference((MemberReferenceHandle)handle);
                    return IsTargetType(reader, memberReference.Parent, targetType);
            }

            return false;
        }

        private static bool TypeNameMatches(string namespaceName, string typeName, Type targetType)
        {
            for (var type = targetType; type != null; type = type.BaseType)
            {
                if (namespaceName == type.Namespace && typeName == type.Name)
                {
                    return true;
                }
            }

            foreach (var interfaceType in targetType.GetInterfaces())
            {
                if (namespaceName == interfaceType.Namespace && typeName == interfaceType.Name)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetOperandSize(int opcode, byte[] il, int position)
        {
            var opCode = GetOpCode(opcode);
            switch (opCode.OperandType)
            {
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;

                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;

                case OperandType.InlineSwitch:
                    if (position + 3 >= il.Length)
                    {
                        return il.Length - position;
                    }

                    var count = il[position] |
                                (il[position + 1] << 8) |
                                (il[position + 2] << 16) |
                                (il[position + 3] << 24);
                    if (count < 0 || count > (il.Length - position - 4) / 4)
                    {
                        return il.Length - position;
                    }

                    return 4 + (count * 4);

                case OperandType.InlineVar:
                    return 2;

                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;

                default:
                    return 0;
            }
        }

        private static OpCode GetOpCode(int opcode)
        {
            return opcode > 0xFF ? MultiByteOpCodes[opcode & 0xFF] : SingleByteOpCodes[opcode];
        }
    }
}
