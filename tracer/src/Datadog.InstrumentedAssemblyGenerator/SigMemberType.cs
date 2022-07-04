using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Datadog.InstrumentedAssemblyGenerator
{
    /// <summary>
    /// Responsible to create ea type sig from a textual representation of a metadata signature so we can create types and add them to metadata
    /// </summary>
    internal class SigMemberType
    {
        private readonly bool _isElementType;
        private readonly string[] _type;
        internal string Name { get; }
        internal TypeSig TypeSig { get; private set; }

        public SigMemberType(string type)
        {
            _isElementType = type.StartsWith("0x");
            Name = type;
            _type = _isElementType ? type.Split(new[] { '?', '<', '>' , ',' }, StringSplitOptions.RemoveEmptyEntries) : new[] { type };
        }

        internal TypeSig GetTypeSig(ModuleDef module, InstrumentedAssemblyGeneratorContext context, Dictionary<string, ITypeDefOrRef> importedTypes = null, int offset = 0)
        {
            if (TypeSig != null)
            {
                return TypeSig;
            }

            return TypeSig = _isElementType ?
                       (TypeSig) CreateSigByElementType(ref offset, module, context, importedTypes) :
                       (TypeSig) CreateSigByTypeName();
        }

        private IFullName CreateSigByTypeName()
        {
            return new TypeNameSig(_type[0]);
        }

        private IFullName CreateSigByElementType(ref int offset, ModuleDef module, InstrumentedAssemblyGeneratorContext context, Dictionary<string, ITypeDefOrRef> importedTypes = null)
        {
            TypeSig toReturn = null;
            var elementType = (ElementType) Convert.ToInt32(_type[offset], 16);
            offset++; // increment element type
            switch (elementType)
            {
                case ElementType.Void:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.Void.TypeDefOrRef, elementType);
                    break;
                case ElementType.Boolean:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.Boolean.TypeDefOrRef, elementType);
                    break;
                case ElementType.Char:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.Char.TypeDefOrRef, elementType);
                    break;
                case ElementType.I1:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.SByte.TypeDefOrRef, elementType);
                    break;
                case ElementType.U1:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.Byte.TypeDefOrRef, elementType);
                    break;
                case ElementType.I2:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.Int16.TypeDefOrRef, elementType);
                    break;
                case ElementType.U2:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.UInt16.TypeDefOrRef, elementType);
                    break;
                case ElementType.I4:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.Int32.TypeDefOrRef, elementType);
                    break;
                case ElementType.U4:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.UInt32.TypeDefOrRef, elementType);
                    break;
                case ElementType.I8:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.Int64.TypeDefOrRef, elementType);
                    break;
                case ElementType.U8:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.UInt64.TypeDefOrRef, elementType);
                    break;
                case ElementType.R4:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.Single.TypeDefOrRef, elementType);
                    break;
                case ElementType.R8:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.Double.TypeDefOrRef, elementType);
                    break;
                case ElementType.String:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.String.TypeDefOrRef, elementType);
                    break;
                case ElementType.I:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.IntPtr.TypeDefOrRef, elementType);
                    break;
                case ElementType.U:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.UIntPtr.TypeDefOrRef, elementType);
                    break;
                case ElementType.Object:
                    toReturn = new CorLibTypeSig(module.CorLibTypes.Object.TypeDefOrRef, elementType);
                    break;
                case ElementType.SZArray:
                    toReturn = new SZArraySig((TypeSig) CreateSigByElementType(ref offset, module, context, importedTypes));
                    break;
                case ElementType.Var:
                    toReturn = new GenericVar(Convert.ToInt32(_type[offset]));
                    offset++;
                    break;
                case ElementType.ByRef:
                    toReturn = new ByRefSig((TypeSig) CreateSigByElementType(ref offset, module, context, importedTypes));
                    break;
                case ElementType.MVar:
                    toReturn = new GenericMVar(Convert.ToInt32(_type[offset]));
                    offset++;
                    break;

                case ElementType.ValueType:
                {
                    var type = FindType(ref offset, module, context, importedTypes);
                    toReturn = new ValueTypeSig(type);
                    break;
                }

                case ElementType.Class:
                {
                    var type = FindType(ref offset, module, context, importedTypes);
                    toReturn = new ClassSig(type);
                    break;
                }

                case ElementType.GenericInst:
                {
                    var genericType = CreateSigByElementType(ref offset, module, context, importedTypes);
                    var tTypes = new List<TypeSig>();
                    while (offset < _type.Length)
                    {
                        tTypes.Add((TypeSig) CreateSigByElementType(ref offset, module, context, importedTypes));
                    }
                    toReturn = new GenericInstSig((ClassOrValueTypeSig) genericType, tTypes);
                    break;
                }

                case ElementType.Array:
                case ElementType.End:
                case ElementType.TypedByRef:
                case ElementType.Ptr:
                case ElementType.FnPtr:
                case ElementType.CModReqd:
                case ElementType.CModOpt:
                case ElementType.Internal:
                case ElementType.Module:
                case ElementType.Sentinel:
                case ElementType.Pinned:
                    Logger.Warn($"Unhandled element type: {elementType}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementType), elementType, null);
            }
            return toReturn;
        }

        private ITypeDefOrRef FindType(ref int offset, ModuleDef module, InstrumentedAssemblyGeneratorContext context, Dictionary<string, ITypeDefOrRef> importedTypes)
        {
            var originalToken = new Token(Convert.ToUInt32(_type[offset], 16));
            // increment token and type name
            offset++;
            offset++;
            var instrumentedModuleTokens = context.InstrumentedModulesTypesTokens[(module.Name, module.Mvid)];
            var originalModuleTokens = context.OriginalModulesTypesTokens[(module.Name, module.Mvid)];
            if (!instrumentedModuleTokens.TokensAndNames.TryGetValue(originalToken, out MetadataMember member))
            {
                originalModuleTokens.TokensAndNames.TryGetValue(originalToken, out member);
            }

            ITypeDefOrRef type = null;
            if (member != null)
            {
                type = context.ResolveInstrumentedMappedType(module, member, originalToken, importedTypes);
            }

            if (type == null && member != null)
            {
                importedTypes?.TryGetValue(member.FullName, out type);
            }

            type ??= module.CorLibTypes.Object.TypeDefOrRef;
            return type;
        }
    }

    /// <summary>
    /// Represent TypeSig that already exist in the module so we don't need an actual signature to create it but just the type full name
    /// </summary>
    internal class TypeNameSig : TypeSig, IFullName
    {
        public TypeNameSig(string fullName)
        {
            FullName = fullName;
            Name = fullName;
        }

        public new string FullName { get; }
        public override TypeSig Next { get; }
        public override ElementType ElementType { get; } // in case we have to use it, we should add this info (element type of added TypeDef) in the native side
        public UTF8String Name { get; set; }
    }
}