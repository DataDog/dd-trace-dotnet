// <copyright file="MethodBodies.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Pdb;
using dnlib.DotNet.Writer;
using dnlib.IO;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

internal static unsafe class MethodBodies
{
    /// <summary>
    /// Returns the raw method body (header, code and extra sections) as stored in the PE image, like
    /// ICorProfilerInfo::GetILFunctionBody.
    /// </summary>
    public static byte[]? ReadOriginal(ModuleState state, uint methodRid)
    {
        if (!state.Tables.TryReadMethodRow(methodRid, out var row) || row.RVA == 0)
        {
            return null;
        }

        var image = state.Metadata.PEImage;
        var reader = image.CreateReader((dnlib.PE.RVA)row.RVA);
        var start = reader.Position;
        var size = ComputeSize(ref reader);
        reader.Position = start;
        return reader.ReadBytes(size);
    }

    public static int ComputeSize(byte* body)
    {
        if ((body[0] & 3) == 2)
        {
            return 1 + (body[0] >> 2);
        }

        var flags = *(ushort*)body;
        var position = ((flags >> 12) * 4) + (int)*(uint*)(body + 4);
        if ((flags & 0x8) == 0)
        {
            return position;
        }

        while (true)
        {
            position = (position + 3) & ~3;
            var kind = body[position];
            var sectionSize = (kind & 0x40) != 0 ? body[position + 1] | (body[position + 2] << 8) | (body[position + 3] << 16) : body[position + 1];
            position += sectionSize;
            if ((kind & 0x80) == 0)
            {
                return position;
            }
        }
    }

    private static int ComputeSize(ref DataReader reader)
    {
        var start = reader.Position;
        var first = reader.ReadByte();
        if ((first & 3) == 2)
        {
            // Tiny header: 1 byte header + code
            return 1 + (first >> 2);
        }

        reader.Position = start;
        var flags = reader.ReadUInt16();
        var headerSize = (flags >> 12) * 4;
        reader.ReadUInt16(); // max stack
        var codeSize = reader.ReadUInt32();
        reader.Position = start + (uint)headerSize + codeSize;
        if ((flags & 0x8) == 0)
        {
            return (int)(reader.Position - start);
        }

        // Extra data sections (exception handling), each aligned to 4 bytes.
        while (true)
        {
            reader.Position = (reader.Position + 3) & ~3u;
            var kind = reader.ReadByte();
            uint sectionSize;
            if ((kind & 0x40) != 0)
            {
                // Fat section: 3 byte size
                sectionSize = reader.ReadByte() | ((uint)reader.ReadByte() << 8) | ((uint)reader.ReadByte() << 16);
            }
            else
            {
                sectionSize = reader.ReadByte();
                reader.ReadUInt16();
            }

            reader.Position = reader.Position - 4 + sectionSize;
            if ((kind & 0x80) == 0)
            {
                break;
            }
        }

        return (int)(reader.Position - start);
    }

    /// <summary>
    /// Converts a raw method body produced by the native rewriter into a dnlib body, resolving both original and
    /// newly defined tokens.
    /// </summary>
    public static CilBody ToCilBody(ModuleState state, MethodDef method, byte[] rawBody)
    {
        var context = new GenericParamContext(method.DeclaringType, method);
        var reader = ByteArrayDataReaderFactory.CreateReader(rawBody);
        return MethodBodyReader.CreateCilBody(new OperandResolver(state), reader, method.Parameters, context);
    }

    /// <summary>
    /// Replaces the bodies of the methods the native rewriter instrumented and returns them.
    /// </summary>
    public static List<MethodDef> ApplyNewBodies(ModuleState state)
    {
        var methods = new List<MethodDef>();
        var sequencePoints = 0;
        foreach (var (rid, raw) in state.NewBodies)
        {
            var method = state.Module.ResolveMethod(rid);
            var original = method.Body;
            var rewritten = ToCilBody(state, method, raw);

            // The PDB keeps the source lines of the rewritten methods (stack traces, debuggers, ILC's debug information).
            if (original is not null)
            {
                sequencePoints += SequencePointTransfer.Transfer(original, rewritten);
            }

            method.Body = rewritten;
            methods.Add(method);
        }

        if (sequencePoints > 0)
        {
            AotLog.Debug($"{state.AssemblyName}: {sequencePoints} sequence points moved to the rewritten methods");
        }

        return methods;
    }

    /// <summary>
    /// Empties methods (test only: the definitions injection of CallTargetNativeTest must not run again).
    /// </summary>
    public static void Neutralize(ModuleState state, IReadOnlyCollection<string> names)
    {
        foreach (var name in names)
        {
            var separator = name.IndexOf("::", StringComparison.Ordinal);
            var type = state.Module.Find(name.Substring(0, separator), isReflectionName: true);
            var method = type?.FindMethod(name.Substring(separator + 2));
            if (method != null)
            {
                method.Body = new CilBody();
                method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                AotLog.Info($"Neutralized {name} (test only)");
            }
        }
    }

    public static void Save(ModuleState state, string outputPath)
    {
        var logger = new WriterErrors();
        var options = new ModuleWriterOptions(state.Module)
        {
            // Only portable PDBs: a Windows PDB needs the Windows symbol writer, and ILC only reads portable ones.
            WritePdb = state.Module.PdbState?.PdbFileKind is PdbFileKind.PortablePDB or PdbFileKind.EmbeddedPortablePDB,
            Logger = logger,
        };

        // The rewritten bodies keep the max stack the native rewriter computed; generated bodies set their own.
        options.MetadataOptions.Flags |= MetadataFlags.KeepOldMaxStack;
        state.Module.Write(outputPath, options);
        foreach (var error in logger.Errors.Take(10))
        {
            // E.g. a type of another module that wasn't imported: dnlib writes a nil token, and the assembly is invalid.
            AotLog.Error($"{state.Module.Assembly?.Name}: {error}");
        }

        // dnlib 3.3 writes some portable PDBs that can't be read (log4net 3.0.0), and ILC fails on them: the module goes
        // without symbols instead.
        var pdbPath = Path.ChangeExtension(outputPath, ".pdb");
        if (options.WritePdb && state.Module.PdbState?.PdbFileKind == PdbFileKind.PortablePDB && File.Exists(pdbPath) && !IsReadablePortablePdb(pdbPath))
        {
            File.Delete(pdbPath);
            AotLog.Warn($"{state.Module.Assembly?.Name}: the PDB written for the instrumented assembly can't be read; it is left out");
        }
    }

    private static bool IsReadablePortablePdb(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
            provider.GetMetadataReader();
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// The errors dnlib reports while writing a module (it writes the module anyway).
    /// </summary>
    private sealed class WriterErrors : ILogger
    {
        public List<string> Errors { get; } = new();

        public void Log(object sender, LoggerEvent loggerEvent, string format, params object[] args)
        {
            if (loggerEvent == LoggerEvent.Error)
            {
                Errors.Add(string.Format(format, args));
            }
        }

        public bool IgnoresEvent(LoggerEvent loggerEvent) => loggerEvent != LoggerEvent.Error;
    }

    private sealed class OperandResolver : IInstructionOperandResolver
    {
        private readonly ModuleState _state;

        public OperandResolver(ModuleState state)
        {
            _state = state;
        }

        public IMDTokenProvider ResolveToken(uint token, GenericParamContext gpContext) => _state.Resolve(token, gpContext);

        public string ReadUserString(uint token)
        {
            var offset = token & 0x00FFFFFF;
            lock (_state.Sync)
            {
                if (_state.UserStrings.TryGetValue(offset, out var value))
                {
                    return value;
                }
            }

            return _state.Metadata.USStream.Read(offset);
        }
    }
}
#endif
