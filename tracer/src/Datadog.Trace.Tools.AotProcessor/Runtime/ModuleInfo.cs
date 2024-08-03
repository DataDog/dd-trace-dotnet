using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Tools.AotProcessor.Interfaces;
using Mono.Cecil;

namespace Datadog.Trace.Tools.AotProcessor.Runtime;

internal class ModuleInfo
{
    private Dictionary<int, MemberInfo> members = new Dictionary<int, MemberInfo>();

    public ModuleInfo(ModuleDefinition definition, int id, AssemblyInfo assembly, string path, COR_PRF_MODULE_FLAGS flags = COR_PRF_MODULE_FLAGS.COR_PRF_MODULE_DISK)
    {
        Definition = definition;
        Id = new ModuleId(id);
        Assembly = assembly;
        Path = path;
        Flags = flags;
        MetadataImport = new ModuleMetadata(this);
    }

    public ModuleDefinition Definition { get; }
    public ModuleId Id { get; }
    public AssemblyInfo Assembly { get; }
    public string Path { get; }

    public COR_PRF_MODULE_FLAGS Flags { get; }

    public ModuleMetadata MetadataImport { get; }

    public MemberInfo? GetMember(MemberReference definition)
    {
        if (definition.Module != Definition) { throw new ArgumentException("Member does not belong to this module"); }
        if (members.TryGetValue(definition.MetadataToken.ToInt32(), out var res))
        {
            return res;
        }

        MemberInfo member;
        if (definition is MethodDefinition methodDefinition)
        {
            member = new MethodInfo(methodDefinition, definition.MetadataToken.ToInt32(), this);
        }
        else
        {
            member = new MemberInfo(definition, definition.MetadataToken.ToInt32(), this);
        }

        members[member.Id.Value] = member;
        return member;
    }

    public MemberInfo? GetMember(int tokenId)
    {
        if (members.TryGetValue(tokenId, out var res))
        {
            return res;
        }

        var definition = Definition.LookupToken(tokenId);

        if (definition is FieldDefinition fieldDef)
        {
            res = new FieldInfo(fieldDef, tokenId, this);
        }
        else if (definition is MethodDefinition methodDef)
        {
            res = new MethodInfo(methodDef, tokenId, this);
        }
        else if (definition is GenericInstanceMethod methodSpec)
        {
            res = new MethodSpecInfo(methodSpec, tokenId, this);
        }
        else if (definition is MethodReference methodRef)
        {
            res = new MemberInfo(methodRef, tokenId, this);
        }

        if (res is not null)
        {
            members[tokenId] = res;
        }

        return res;
    }
}
