using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using static Datadog.InstrumentedAssemblyGenerator.InstrumentedAssemblyGeneratorConsts;

namespace Datadog.InstrumentedAssemblyGenerator
{
    internal class ModuleTokensMapping
    {
        internal ModuleTokensMapping(Guid moduleMvid, string moduleName, Func<Dictionary<Token, MetadataMember>> creator)
        {
            ModuleMvid = moduleMvid;
            ModuleName = moduleName;
            TokensAndNames = creator();
        }

        internal Guid? ModuleMvid { get; }
        internal string ModuleName { get; }

        internal Dictionary<Token, MetadataMember> TokensAndNames { get; }

        /// <summary>
        /// File pattern: mvid@moduleToken@moduleName.modulemembers;
        /// We can't trust the 'moduleName' part of the file name because invalid file characters might have been removed in native side,
        /// so I read the module name from the first line on the file
        /// <example>
        /// {CA5FFC62-F144-4B68-BC38-30977E9674D7}@7ffcc2253f68@vstest.console.exe.modulemembers
        /// </example>
        /// </summary>
        /// <param name="filePath">.modulemembers file to read and create map</param>
        /// <param name="modulesToRead">modules to read or if null, read everything</param>
        /// <returns></returns>
        internal static ModuleTokensMapping ReadFromFile(string filePath, string[] modulesToRead)
        {
            string[] parts = Path.GetFileNameWithoutExtension(filePath).Split(MetadataValueSeparator.ToCharArray());
            if (parts.Length != ModuleMembersFileParts)
            {
                throw new ArgumentException(nameof(filePath));
            }

            // Skip RefEmit_InMemoryManifestModule and others
            if (!parts[1].EndsWith(".dll") && !parts[1].EndsWith(".exe"))
            {
                return null;
            }

            if (modulesToRead?.Any() == true && !modulesToRead.Contains(parts[1], StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            return new ModuleTokensMapping(
                Guid.Parse(parts[0]),
                parts[1],
                () => ReadFromFileLinesCreator(filePath));
        }

        internal static Dictionary<Token, MetadataMember> ReadFromFileLinesCreator(string filePath)
        {
            var lines = File.ReadLines(filePath).ToList();
            var dic = new Dictionary<Token, MetadataMember>();
            foreach (string line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                // line in the following pattern: Token = Type.Method
                string[] parts = line.Split('=');
                if (parts.Length != 2)
                {
                    throw new ArgumentException($"Invalid file format, should be: Token = Type.Method. File: '{filePath}'");
                }
                var token = new Token(uint.Parse(parts[0], System.Globalization.NumberStyles.HexNumber));
                if (token.IsNull)
                {
                    continue;
                }

                var memberName = MetadataMember.Create(token, parts[1]);
                if (!memberName.IsEmpty)
                {
                    dic[token] = memberName;
                }
            }

            return dic;
        }

        /// <summary>
        /// Create "token to module" map from a loaded module
        /// </summary>
        internal static ModuleTokensMapping CreateFromModule(ModuleDefMD module)
        {
            if (module.Mvid == null)
            {
                return null;
            }

            return new ModuleTokensMapping(
                (Guid) module.Mvid,
                module.FullName,
                ReadFromModuleCreator);

            Dictionary<Token, MetadataMember> ReadFromModuleCreator()
            {
                var dic = new Dictionary<Token, MetadataMember>();
                foreach (var typeDef in module.GetTypes())
                {
                    if (typeDef.FullName.ToLower().Contains("__ddvoidmethodtype__"))
                    {

                    }
                    var token = new Token(typeDef.MDToken.Raw);
                    if (token.IsNull)
                    {
                        continue;
                    }

                    var memberName = MetadataMember.Create(token, typeDef.FullName);
                    if (!memberName.IsEmpty)
                    {
                        dic.Add(token, memberName);
                    }

                    foreach (var methodDef in typeDef.Methods)
                    {
                        token = new Token(methodDef.MDToken.Raw);
                        if (token.IsNull)
                        {
                            continue;
                        }

                        memberName = MetadataMember.Create(token, methodDef.FullName);
                        if (!memberName.IsEmpty)
                        {
                            dic.Add(token, memberName);
                        }
                    }

                    foreach (var fieldDef in typeDef.Fields)
                    {
                        token = new Token(fieldDef.MDToken.Raw);
                        if (token.IsNull)
                        {
                            continue;
                        }

                        memberName = MetadataMember.Create(token, fieldDef.FullName);
                        if (!memberName.IsEmpty)
                        {
                            dic.Add(token, memberName);
                        }
                    }
                }

                foreach (var typeRef in module.GetTypeRefs())
                {
                    var token = new Token(typeRef.MDToken.Raw);
                    if (token.IsNull)
                    {
                        continue;
                    }

                    var memberName = MetadataMember.Create(token, typeRef.FullName);
                    if (!memberName.IsEmpty)
                    {
                        dic.Add(token, memberName);
                    }
                }

                foreach (var memberRef in module.GetMemberRefs())
                {
                    var token = new Token(memberRef.MDToken.Raw);
                    if (token.IsNull)
                    {
                        continue;
                    }

                    var memberName = MetadataMember.Create(token, memberRef.FullName);
                    if (!memberName.IsEmpty)
                    {
                        dic.Add(token, memberName);
                    }
                }

                foreach (var memberRef in module.GetModuleRefs())
                {
                    var token = new Token(memberRef.MDToken.Raw);
                    if (token.IsNull)
                    {
                        continue;
                    }

                    var memberName = MetadataMember.Create(token, memberRef.FullName);
                    if (!memberName.IsEmpty)
                    {
                        dic.Add(token, memberName);
                    }
                }

                foreach (var memberRef in module.GetAssemblyRefs())
                {
                    var token = new Token(memberRef.MDToken.Raw);
                    if (token.IsNull)
                    {
                        continue;
                    }

                    var memberName = MetadataMember.Create(token, memberRef.FullName);
                    if (!memberName.IsEmpty)
                    {
                        dic.Add(token, memberName);
                    }
                }

                foreach (var memberRef in module.ExportedTypes)
                {
                    var token = new Token(memberRef.MDToken.Raw);
                    if (token.IsNull)
                    {
                        continue;
                    }

                    var memberName = MetadataMember.Create(token, memberRef.FullName);
                    if (!memberName.IsEmpty)
                    {
                        dic.Add(token, memberName);
                    }
                }

                foreach (var corlibTypeProperty in module.CorLibTypes.GetType().GetProperties())
                {
                    if (!(corlibTypeProperty.GetValue(module.CorLibTypes) is CorLibTypeSig sig))
                    {
                        continue;
                    }

                    var token = new Token(sig.TypeDefOrRef.MDToken.Raw);
                    if (token.IsNull)
                    {
                        continue;
                    }

                    dic.Add(token, MetadataMember.Create(token, sig.TypeDefOrRef.Name));
                }

                for (uint i = 0; i <= module.Metadata.TablesStream.TypeSpecTable.Rows; i++)
                {
                    if (module.Metadata.TablesStream.TryReadTypeSpecRow(i, out var row))
                    {
                        var typeSpec = module.ResolveTypeSpec(i);
                        var token = new Token(typeSpec.MDToken.Raw);
                        if (token.IsNull || token.Table != MetadataTable.TypeSpec)
                        {
                            continue;
                        }
                        dic.Add(token, MetadataMember.Create(token, typeSpec.FullName));
                    }
                }

                for (uint i = 0; i <= module.Metadata.TablesStream.MethodSpecTable.Rows; i++)
                {
                    if (module.Metadata.TablesStream.TryReadMethodSpecRow(i, out var row))
                    {
                        var methodSpec = module.ResolveMethodSpec(i);
                        var token = new Token(methodSpec.MDToken.Raw);
                        if (token.IsNull || token.Table != MetadataTable.MethodSpec)
                        {
                            continue;
                        }
                        dic.Add(token, MetadataMember.Create(token, methodSpec.FullName));
                    }
                }

                var usReader = module.USStream.CreateReader();
                uint length;
                for (uint offset = 1; offset < module.USStream.StreamLength; offset += length + 1)
                {
                    string us = string.Empty;
                    usReader.Position = offset & 0xffffff;
                    if (usReader.TryReadCompressedUInt32(out length) &&
                        usReader.CanRead(length))
                    {
                        try
                        {
                            us = usReader.ReadUtf16String((int) (length / 2U));
                        }
                        catch
                        {
                            // ignored: If we failed to read, 'us' will be string.Empty and we will add it as is.
                            // TODO: Do we want to put a meaning string to identify it later in the code?
                        }
                    }

                    var token = new Token(0x70000000 | offset);
                    dic.Add(token, MetadataMember.Create(token, us));
                }

                return dic;
            }
        }
    }
}
