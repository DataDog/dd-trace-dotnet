using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.IO;

namespace Datadog.InstrumentedAssemblyGenerator
{
    /// <summary>
    /// Read the instrumented method and replace old tokens with updated tokens
    /// </summary>
    internal class InstrumentedMethodBodyReader : MethodBodyReaderBase
    {
        private readonly GenericParamContext _genericContext;
        private readonly InstrumentedAssemblyGeneratorContext _context;
        private readonly ModuleTokensMapping _instrumentedModuleTokens;
        private readonly ModuleTokensMapping _originalModuleTokens;
        private readonly ModuleDef _module;
        private readonly IList<TypeSig> _locals;
        private bool _hasReadHeader;
        private byte _headerSize;
        private ushort _flags;
        private ushort _maxStack;
        private uint _codeSize;
        private uint _localVarSigTok;
        private uint _startOfHeader;
        private uint _totalBodySize;

        internal InstrumentedMethodBodyReader(
            ModuleDefMD module,
            DataReader codeReader,
            ParameterList parameters,
            GenericParamContext genericContext,
            InstrumentedAssemblyGeneratorContext context,
            ModuleTokensMapping instrumentedModuleTokens,
            ModuleTokensMapping originalModuleTokens,
            IList<TypeSig> locals)
            : base(codeReader, parameters)
        {
            _module = module;
            _genericContext = genericContext;
            _context = context;
            _instrumentedModuleTokens = instrumentedModuleTokens;
            _originalModuleTokens = originalModuleTokens;
            _locals = locals;
        }

        protected override IField ReadInlineField(Instruction instr)
        {
            var originalToken = new Token(reader.ReadUInt32());
            var member = GetMemberFromMap(originalToken);
            var field = member == null ? null : _context.ResolveInstrumentedMappedField(_module, originalToken, member, _genericContext);
            ValidateTokenProvider(field, originalToken);
            return field;
        }

        protected override IMethod ReadInlineMethod(Instruction instr)
        {
            var originalToken = new Token(reader.ReadUInt32());

            var member = GetMemberFromMap(originalToken);
            var method = member == null ? null : _context.ResolveInstrumentedMappedMethod(_module, member, originalToken, _genericContext);
            ValidateTokenProvider(method, originalToken);
            return method;
        }

        protected override ITypeDefOrRef ReadInlineType(Instruction instr)
        {
            var originalToken = new Token(reader.ReadUInt32());
            var member = GetMemberFromMap(originalToken);
            var type = member == null ? null : _context.ResolveInstrumentedMappedType(_module, member, originalToken, null);
            ValidateTokenProvider(type, originalToken);
            return type;
        }

        protected override MethodSig ReadInlineSig(Instruction instr)
        {
            var standAloneSig = _module.ResolveToken(reader.ReadUInt32(), _genericContext) as StandAloneSig;
            if (standAloneSig == null)
            {
                Logger.Error("Could not find a StandAloneSig.");
                return null;
            }

            MethodSig methodSig = standAloneSig.MethodSig;
            if (methodSig != null)
            {
                methodSig.OriginalToken = standAloneSig.MDToken.Raw;
            }

            return methodSig;
        }

        protected override string ReadInlineString(Instruction instr)
        {
            var originalToken = new Token(reader.ReadUInt32());
            var member = GetMemberFromMap(originalToken);
            return member?.FullName ?? "";
        }

        protected override ITokenOperand ReadInlineTok(Instruction instr)
        {
            var originalToken = new Token(reader.ReadUInt32());
            var member = GetMemberFromMap(originalToken);
            if (member == null)
            {
                return null;
            }

            switch (originalToken.Table)
            {
                case MetadataTable.Field:
                {
                    var instrumentedResolvedField = _context.ResolveInstrumentedMappedField(_module, originalToken, member, _genericContext);
                    if (instrumentedResolvedField != null)
                    {
                        return instrumentedResolvedField;
                    }
                    break;
                }
                case MetadataTable.TypeDef:
                case MetadataTable.TypeRef:
                case MetadataTable.TypeSpec:
                {
                    var instrumentedResolved = _context.ResolveInstrumentedMappedType(_module, member, originalToken, null);
                    if (instrumentedResolved != null)
                    {
                        return instrumentedResolved;
                    }
                    break;
                }
                case MetadataTable.Method:
                case MetadataTable.MethodSpec:
                case MetadataTable.MemberRef:
                {
                    var instrumentedResolvedMethod = _context.ResolveInstrumentedMappedMethod(_module, member, originalToken, _genericContext);
                    if (instrumentedResolvedMethod != null)
                    {
                        return instrumentedResolvedMethod;
                    }
                    break;
                }
                default:
                {
                    Logger.Error($"{nameof(ReadInlineTok)}: {originalToken.Table} is not implemented");
                    break;
                }


            }
            Logger.Error($"{nameof(ReadInlineTok)}: Can't resolve {originalToken}");
            return null;
        }

        private void ValidateTokenProvider(IMDTokenProvider token, Token originalToken, [CallerMemberName] string caller = "")
        {
            if (token == null)
            {
                Logger.Error($"{caller}: Could not find a matching member. " +
                             $"Seems that token {originalToken} does not exist in (a) the instrumented metadata ({_module.Name}{InstrumentedAssemblyGeneratorConsts.ModuleMembersFileExtension}) nor in (b) the original assembly metadata." +
                             "It can occur (a) if the defined MethodX \\ MemberRef had some invalid data hence the InstrumentedAssemblyGenerator in the CLR Profiler failed to write it to disk" +
                             " or (b) if we failed to parse the member and add it to metadata map.");
            }
        }

        internal bool Read()
        {
            try
            {
                if (!ReadHeader())
                {
                    return false;
                }

                SetLocals(ReadLocals());
                ReadInstructionsNumBytes(_codeSize);
                ReadExceptionHandlers(out _totalBodySize);
                return true;
            }
            catch (InvalidMethodException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private void ReadExceptionHandlers(out uint totalBodySize)
        {
            if ((_flags & 8) == 0)
            {
                totalBodySize = _startOfHeader == uint.MaxValue ? 0 : reader.Position - _startOfHeader;
                return;
            }

            var ehReader = reader;
            ehReader.Position = ehReader.Position + 3 & ~3U;

            // Only read the first one. Any others aren't used.
            byte b = ehReader.ReadByte();
            if ((b & 0x3F) != 1)
            {
                totalBodySize = _startOfHeader == uint.MaxValue ? 0 : reader.Position - _startOfHeader;
                return; // Not exception handler clauses
            }
            if ((b & 0x40) != 0)
            {
                ReadFatExceptionHandlers(ref ehReader);
            }
            else
            {
                ReadSmallExceptionHandlers(ref ehReader);
            }

            totalBodySize = _startOfHeader == uint.MaxValue ? 0 : ehReader.Position - _startOfHeader;
        }

        private void ReadFatExceptionHandlers(ref DataReader ehReader)
        {
            ehReader.Position--;
            int num = (ushort) ((ehReader.ReadUInt32() >> 8) / 24);
            for (int i = 0; i < num; i++)
            {
                var eh = new ExceptionHandler((ExceptionHandlerType) ehReader.ReadUInt32());
                uint offs = ehReader.ReadUInt32();
                eh.TryStart = GetInstruction(offs);
                eh.TryEnd = GetInstruction(offs + ehReader.ReadUInt32());
                offs = ehReader.ReadUInt32();
                eh.HandlerStart = GetInstruction(offs);
                eh.HandlerEnd = GetInstruction(offs + ehReader.ReadUInt32());
                if (eh.HandlerType == ExceptionHandlerType.Catch)
                {
                    eh.CatchType = GetCatchType(new Token(ehReader.ReadUInt32()));
                }
                else if (eh.HandlerType == ExceptionHandlerType.Filter)
                {
                    eh.FilterStart = GetInstruction(ehReader.ReadUInt32());
                }
                else
                {
                    ehReader.ReadUInt32();
                }

                Add(eh);
            }
        }

        private void ReadSmallExceptionHandlers(ref DataReader ehReader)
        {
            int num = (ushort) (ehReader.ReadByte() / 12);
            ehReader.Position += 2;
            for (int i = 0; i < num; i++)
            {
                var eh = new ExceptionHandler((ExceptionHandlerType) ehReader.ReadUInt16());
                uint offs = ehReader.ReadUInt16();
                eh.TryStart = GetInstruction(offs);
                eh.TryEnd = GetInstruction(offs + ehReader.ReadByte());
                offs = ehReader.ReadUInt16();
                eh.HandlerStart = GetInstruction(offs);
                eh.HandlerEnd = GetInstruction(offs + ehReader.ReadByte());
                if (eh.HandlerType == ExceptionHandlerType.Catch)
                {
                    eh.CatchType = GetCatchType(new Token(ehReader.ReadUInt32()));
                }
                else if (eh.HandlerType == ExceptionHandlerType.Filter)
                {
                    eh.FilterStart = GetInstruction(ehReader.ReadUInt32());
                }
                else
                {
                    ehReader.ReadUInt32();
                }

                Add(eh);
            }
        }

        private ITypeDefOrRef GetCatchType(Token originalToken)
        {
            var member = GetMemberFromMap(originalToken);
            if (member != null)
            {
                return _context.ResolveInstrumentedMappedType(_module, member, originalToken, null);
            }
            else
            {
                Logger.Error($"Can't find catch type. Token: {originalToken}");
                return _module.GetTypeRefs().First(t => t.FullName == "System.Exception");
            }
        }

        private IList<TypeSig> ReadLocals()
        {
            return _locals;
        }

        private bool ReadHeader()
        {
            if (_hasReadHeader)
            {
                return true;
            }

            _hasReadHeader = true;

            _startOfHeader = reader.Position;
            byte b = reader.ReadByte();
            switch (b & 7)
            {
                case 2:
                case 6:
                    // Tiny header. [7:2] = code size, max stack is 8, no locals or exception handlers
                    _flags = 2;
                    _maxStack = 8;
                    _codeSize = (uint) (b >> 2);
                    _localVarSigTok = 0;
                    _headerSize = 1;
                    break;

                case 3:
                    // Fat header. Can have locals and exception handlers
                    _flags = (ushort) (reader.ReadByte() << 8 | b);
                    _headerSize = (byte) (_flags >> 12);
                    _maxStack = reader.ReadUInt16();
                    _codeSize = reader.ReadUInt32();
                    _localVarSigTok = reader.ReadUInt32();

                    // The CLR allows the code to start inside the method header. But if it does,
                    // the CLR doesn't read any exceptions.
                    reader.Position = reader.Position - 12 + _headerSize * 4U;
                    if (_headerSize < 3)
                    {
                        _flags &= 0xFFF7;
                    }

                    _headerSize *= 4;
                    break;

                default:
                    return false;
            }

            if ((ulong) reader.Position + _codeSize > reader.Length)
            {
                return false;
            }

            return true;
        }

        internal CilBody CreateCilBody()
        {
            // init locals if it's a tiny method or if the init locals bit is set (fat header):
            bool initLocals = _flags == 2 || (_flags & 0x10) != 0 || locals.Count > 0;
            var cilBody = new CilBody(initLocals, instructions, exceptionHandlers, locals);
            cilBody.HeaderSize = _headerSize;
            cilBody.MaxStack = _maxStack;
            cilBody.LocalVarSigTok = _localVarSigTok;
            try
            {
                cilBody.GetType().GetProperty("MetadataBodySize", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cilBody, _totalBodySize);
            }
            catch (Exception e)
            {
                Logger.Error(e);
                throw;
            }
            instructions = null;
            exceptionHandlers = null;
            locals = null;
            return cilBody;
        }

        private MetadataMember GetMemberFromMap(Token originalToken)
        {
            if (!_instrumentedModuleTokens.TokensAndNames.TryGetValue(originalToken, out MetadataMember member))
            {
                _originalModuleTokens.TokensAndNames.TryGetValue(originalToken, out member);
            }

            return member;
        }
    }
}