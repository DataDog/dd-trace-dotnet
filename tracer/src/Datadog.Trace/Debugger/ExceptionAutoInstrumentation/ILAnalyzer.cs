// <copyright file="ILAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
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
                if (opcode == 0x28 || opcode == 0x6F)
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

            if (StringUtil.IsNullOrEmpty(module.FullyQualifiedName) || !File.Exists(module.FullyQualifiedName))
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
                        return false;
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
            switch (opcode)
            {
                case 0x20: // ldc.i4
                case 0x22: // ldc.r4
                case 0x27: // jmp
                case 0x29: // calli
                case 0x38: // br
                case 0x39: // brfalse
                case 0x3A: // brtrue
                case 0x3B: // beq
                case 0x3C: // bge
                case 0x3D: // bgt
                case 0x3E: // ble
                case 0x3F: // blt
                case 0x40: // bne.un
                case 0x41: // bge.un
                case 0x42: // bgt.un
                case 0x43: // ble.un
                case 0x44: // blt.un
                case 0x70: // cpobj
                case 0x71: // ldobj
                case 0x72: // ldstr
                case 0x73: // newobj
                case 0x74: // castclass
                case 0x75: // isinst
                case 0x79: // unbox
                case 0x7B: // ldfld
                case 0x7C: // ldflda
                case 0x7D: // stfld
                case 0x7E: // ldsfld
                case 0x7F: // ldsflda
                case 0x80: // stsfld
                case 0x81: // stobj
                case 0x8C: // box
                case 0x8D: // newarr
                case 0x8F: // ldelema
                case 0xA3: // ldelem
                case 0xA4: // stelem
                case 0xA5: // unbox.any
                case 0xC2: // refanyval
                case 0xC6: // mkrefany
                case 0xD0: // ldtoken
                case 0xDD: // leave
                case 0xFE06: // ldftn
                case 0xFE07: // ldvirtftn
                case 0xFE15: // initobj
                case 0xFE16: // constrained
                case 0xFE1C: // sizeof
                    return 4;

                case 0x21: // ldc.i8
                case 0x23: // ldc.r8
                    return 8;

                case 0x45: // switch
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

                case 0xFE09: // ldarg
                case 0xFE0A: // ldarga
                case 0xFE0B: // starg
                case 0xFE0C: // ldloc
                case 0xFE0D: // ldloca
                case 0xFE0E: // stloc
                    return 2;

                case 0x0F: // ldarg.s
                case 0x10: // ldarga.s
                case 0x11: // starg.s
                case 0x12: // ldloc.s
                case 0x13: // ldloca.s
                case 0x14: // stloc.s
                case 0x1F: // ldc.i4.s
                case 0x2B: // br.s
                case 0x2C: // brfalse.s
                case 0x2D: // brtrue.s
                case 0x2E: // beq.s
                case 0x2F: // bge.s
                case 0x30: // bgt.s
                case 0x31: // ble.s
                case 0x32: // blt.s
                case 0x33: // bne.un.s
                case 0x34: // bge.un.s
                case 0x35: // bgt.un.s
                case 0x36: // ble.un.s
                case 0x37: // blt.un.s
                case 0xDE: // leave.s
                case 0xFE12: // unaligned
                    return 1;

                default:
                    return 0;
            }
        }
    }
}
