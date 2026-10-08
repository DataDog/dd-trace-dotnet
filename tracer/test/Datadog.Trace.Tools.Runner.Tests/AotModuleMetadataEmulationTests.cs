// <copyright file="AotModuleMetadataEmulationTests.cs" company="Datadog">
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
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Datadog.Trace.Tools.Runner.Aot.Native;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Checks the IMetaData* emulation the native tracer uses offline against System.Reflection.Metadata, which reads the
/// same tables independently, and checks that rows defined by the rewriter round-trip like in the CLR (deduplicated,
/// readable back, next RID of their table).
/// </summary>
public sealed unsafe class AotModuleMetadataEmulationTests : IDisposable
{
    private const int NameCapacity = 64 * 1024;

    private readonly EmulatedRuntime _runtime;
    private readonly ModuleMetadata _metadata;
    private readonly PEReader _peReader;
    private readonly MetadataReader _reader;
    private readonly char* _buffer;

    public AotModuleMetadataEmulationTests()
    {
        var path = typeof(Datadog.Trace.Tracer).Assembly.Location;
        _runtime = new EmulatedRuntime(new Version(8, 0, 0));
        var state = _runtime.AddModule(path, writable: false);
        _metadata = _runtime.GetMetadata(state.Id);
        _peReader = new PEReader(File.OpenRead(path));
        _reader = _peReader.GetMetadataReader();
        _buffer = (char*)Marshal.AllocHGlobal(NameCapacity * sizeof(char));
    }

    private delegate HResult EnumFunction<T>(HCORENUM* handle, T* buffer, uint max, uint* count)
        where T : unmanaged;

    public void Dispose()
    {
        Marshal.FreeHGlobal((IntPtr)_buffer);
        _peReader.Dispose();
        _runtime.Dispose();
    }

    [Fact]
    public void TypeDefinitionsMatch()
    {
        var expected = _reader.TypeDefinitions.Select(h => MetadataTokens.GetToken(h)).Where(t => (t & 0xFFFFFF) != 1).ToList();
        Enumerate<MdTypeDef>((HCORENUM* e, MdTypeDef* buffer, uint max, uint* count) => _metadata.EnumTypeDefs(e, buffer, max, count))
           .Select(t => t.Value).Should().Equal(expected);

        foreach (var handle in _reader.TypeDefinitions)
        {
            var definition = _reader.GetTypeDefinition(handle);
            var name = _buffer;
            uint length;
            int flags;
            MdToken extends;
            _metadata.GetTypeDefProps(new MdTypeDef(MetadataTokens.GetToken(handle)), name, NameCapacity, &length, &flags, &extends).Code.Should().Be(0);
            new string(name).Should().Be(FullName(definition.Namespace, definition.Name));
            length.Should().Be((uint)new string(name).Length + 1);
            flags.Should().Be((int)definition.Attributes);
            // Like RegMeta, a null TypeDefOrRef coded index decodes to mdTypeDefNil (0x02000000).
            extends.Value.Should().Be(definition.BaseType.IsNil ? 0x02000000 : MetadataTokens.GetToken(definition.BaseType));

            MdTypeDef enclosing;
            var hr = _metadata.GetNestedClassProps(new MdTypeDef(MetadataTokens.GetToken(handle)), &enclosing);
            if (definition.GetDeclaringType().IsNil)
            {
                hr.Code.Should().Be(NativeBuffers.RecordNotFound);
            }
            else
            {
                enclosing.Value.Should().Be(MetadataTokens.GetToken(definition.GetDeclaringType()));
            }
        }
    }

    [Fact]
    public void MethodsMatch()
    {
        foreach (var typeHandle in _reader.TypeDefinitions.Take(400))
        {
            var type = _reader.GetTypeDefinition(typeHandle);
            var expected = type.GetMethods().Select(h => MetadataTokens.GetToken(h)).ToList();
            var typeToken = new MdTypeDef(MetadataTokens.GetToken(typeHandle));
            Enumerate<MdMethodDef>((HCORENUM* e, MdMethodDef* buffer, uint max, uint* count) =>
            {
                var hr = _metadata.EnumMethods(e, typeToken, buffer, max, out var fetched);
                *count = fetched;
                return hr;
            }).Select(t => t.Value).Should().Equal(expected);

            foreach (var methodHandle in type.GetMethods())
            {
                var method = _reader.GetMethodDefinition(methodHandle);
                var name = _buffer;
                uint length, signatureLength, rva;
                int attributes, implFlags;
                MdToken owner;
                IntPtr signature;
                _metadata.GetMethodProps(new MdMethodDef(MetadataTokens.GetToken(methodHandle)), &owner, name, NameCapacity, &length, &attributes, &signature, &signatureLength, &rva, &implFlags).Code.Should().Be(0);
                new string(name).Should().Be(_reader.GetString(method.Name));
                owner.Value.Should().Be(typeToken.Value);
                attributes.Should().Be((int)method.Attributes);
                implFlags.Should().Be((int)method.ImplAttributes);
                rva.Should().Be((uint)method.RelativeVirtualAddress);
                Bytes(signature, signatureLength).Should().Equal(_reader.GetBlobBytes(method.Signature));
            }
        }
    }

    [Fact]
    public void TypeAndMemberReferencesMatch()
    {
        foreach (var handle in _reader.TypeReferences)
        {
            var reference = _reader.GetTypeReference(handle);
            var name = _buffer;
            uint length;
            MdToken scope;
            _metadata.GetTypeRefProps(new MdTypeRef(MetadataTokens.GetToken(handle)), &scope, name, NameCapacity, &length).Code.Should().Be(0);
            new string(name).Should().Be(FullName(reference.Namespace, reference.Name));
            scope.Value.Should().Be(reference.ResolutionScope.IsNil ? 0 : MetadataTokens.GetToken(reference.ResolutionScope));
        }

        foreach (var handle in _reader.MemberReferences)
        {
            var reference = _reader.GetMemberReference(handle);
            var name = _buffer;
            uint length, signatureLength;
            MdToken parent;
            IntPtr signature;
            _metadata.GetMemberRefProps(new MdMemberRef(MetadataTokens.GetToken(handle)), &parent, name, NameCapacity, &length, &signature, &signatureLength).Code.Should().Be(0);
            new string(name).Should().Be(_reader.GetString(reference.Name));
            parent.Value.Should().Be(MetadataTokens.GetToken(reference.Parent));
            Bytes(signature, signatureLength).Should().Equal(_reader.GetBlobBytes(reference.Signature));
        }
    }

    [Fact]
    public void AssemblyAndAssemblyReferencesMatch()
    {
        var expected = _reader.AssemblyReferences.Select(h => MetadataTokens.GetToken(h)).ToList();
        Enumerate<MdAssemblyRef>((HCORENUM* e, MdAssemblyRef* buffer, uint max, uint* count) =>
        {
            var hr = _metadata.EnumAssemblyRefs(e, buffer, max, out var fetched);
            *count = fetched;
            return hr;
        }).Select(t => t.Value).Should().Equal(expected);

        foreach (var handle in _reader.AssemblyReferences)
        {
            var reference = _reader.GetAssemblyReference(handle);
            var name = _buffer;
            uint length;
            IntPtr key, hash;
            int keyLength, hashLength, flags;
            var metadata = default(AssemblyMetadataRaw);
            _metadata.GetAssemblyRefProps(new MdAssemblyRef(MetadataTokens.GetToken(handle)), &key, &keyLength, name, NameCapacity, &length, (ASSEMBLYMETADATA*)&metadata, &hash, &hashLength, &flags).Code.Should().Be(0);
            new string(name).Should().Be(_reader.GetString(reference.Name));
            new Version(metadata.MajorVersion, metadata.MinorVersion, metadata.BuildNumber, metadata.RevisionNumber).Should().Be(reference.Version);
            Bytes(key, (uint)keyLength).Should().Equal(_reader.GetBlobBytes(reference.PublicKeyOrToken));
            flags.Should().Be((int)reference.Flags);
        }

        var assembly = _reader.GetAssemblyDefinition();
        var assemblyName = _buffer;
        uint assemblyNameLength;
        IntPtr publicKey;
        int publicKeyLength, hashAlgorithm, assemblyFlags;
        var assemblyMetadata = default(AssemblyMetadataRaw);
        _metadata.GetAssemblyProps(new MdAssembly(0x20000001), &publicKey, &publicKeyLength, &hashAlgorithm, assemblyName, NameCapacity, &assemblyNameLength, (ASSEMBLYMETADATA*)&assemblyMetadata, &assemblyFlags).Code.Should().Be(0);
        new string(assemblyName).Should().Be(_reader.GetString(assembly.Name));
        new Version(assemblyMetadata.MajorVersion, assemblyMetadata.MinorVersion, assemblyMetadata.BuildNumber, assemblyMetadata.RevisionNumber).Should().Be(assembly.Version);
        Bytes(publicKey, (uint)publicKeyLength).Should().Equal(_reader.GetBlobBytes(assembly.PublicKey));
    }

    [Fact]
    public void CustomAttributesGenericParametersAndUserStringsMatch()
    {
        foreach (var typeHandle in _reader.TypeDefinitions.Take(300))
        {
            var type = _reader.GetTypeDefinition(typeHandle);
            var owner = new MdToken(MetadataTokens.GetToken(typeHandle));
            var expectedAttributes = type.GetCustomAttributes().Select(h => MetadataTokens.GetToken(h)).ToList();
            Enumerate<MdCustomAttribute>((HCORENUM* e, MdCustomAttribute* buffer, uint max, uint* count) => _metadata.EnumCustomAttributes(e, owner, default, buffer, max, count))
               .Select(t => t.Value).Should().Equal(expectedAttributes);

            foreach (var attributeHandle in type.GetCustomAttributes())
            {
                var attribute = _reader.GetCustomAttribute(attributeHandle);
                MdToken parent, constructor;
                IntPtr blob;
                uint blobLength;
                _metadata.GetCustomAttributeProps(new MdCustomAttribute(MetadataTokens.GetToken(attributeHandle)), &parent, &constructor, &blob, &blobLength).Code.Should().Be(0);
                parent.Value.Should().Be(owner.Value);
                constructor.Value.Should().Be(MetadataTokens.GetToken(attribute.Constructor));
                Bytes(blob, blobLength).Should().Equal(_reader.GetBlobBytes(attribute.Value));
            }

            var expectedParameters = type.GetGenericParameters().Select(h => MetadataTokens.GetToken(h)).ToList();
            var genericParameters = Enumerate<MdGenericParam>((HCORENUM* e, MdGenericParam* buffer, uint max, uint* count) =>
            {
                var hr = _metadata.EnumGenericParams(e, owner, buffer, max, out var fetched);
                *count = fetched;
                return hr;
            });
            genericParameters.Select(t => t.Value).Should().Equal(expectedParameters);
            foreach (var parameter in genericParameters)
            {
                var expected = _reader.GetGenericParameter(MetadataTokens.GenericParameterHandle(parameter.Value & 0xFFFFFF));
                var name = _buffer;
                MdToken parameterOwner;
                _metadata.GetGenericParamProps(parameter, out var sequence, out var flags, &parameterOwner, out _, name, NameCapacity, out _).Code.Should().Be(0);
                new string(name).Should().Be(_reader.GetString(expected.Name));
                sequence.Should().Be((uint)expected.Index);
                flags.Should().Be((int)expected.Attributes);
                parameterOwner.Value.Should().Be(owner.Value);
            }
        }

        var userString = MetadataTokens.UserStringHandle(1);
        var count = 0;
        while (!userString.IsNil && count < 200)
        {
            var expected = _reader.GetUserString(userString);
            var buffer = _buffer;
            uint length;
            _metadata.GetUserString(new MdString(MetadataTokens.GetToken(userString)), buffer, (uint)expected.Length, &length).Code.Should().Be(0);
            length.Should().Be((uint)expected.Length);
            new string(buffer, 0, expected.Length).Should().Be(expected);
            userString = _reader.GetNextHandle(userString);
            count++;
        }

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DefinedRowsRoundTripAndAreDeduplicated()
    {
        var assemblyRefs = _reader.AssemblyReferences.Count;
        var scope = new MdToken(MetadataTokens.GetToken(_reader.AssemblyReferences.First()));

        // DefineAssemblyRef appends a row (the CLR does not deduplicate assembly references by default).
        var assemblyName = "Datadog.Aot.Test.Reference";
        var assemblyMetadata = new AssemblyMetadataRaw { MajorVersion = 1, MinorVersion = 2, BuildNumber = 3, RevisionNumber = 4 };
        var publicKeyToken = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        MdToken assemblyRef;
        fixed (char* name = assemblyName)
        {
            fixed (byte* token = publicKeyToken)
            {
                _metadata.DefineAssemblyRef((IntPtr)token, publicKeyToken.Length, name, (ASSEMBLYMETADATA*)&assemblyMetadata, IntPtr.Zero, 0, 0, &assemblyRef).Code.Should().Be(0);
            }
        }

        assemblyRef.Value.Should().Be(0x23000000 | (assemblyRefs + 1));
        var readName = _buffer;
        uint readLength;
        IntPtr key, hash;
        int keyLength, hashLength, flags;
        var readMetadata = default(AssemblyMetadataRaw);
        _metadata.GetAssemblyRefProps(new MdAssemblyRef(assemblyRef.Value), &key, &keyLength, readName, NameCapacity, &readLength, (ASSEMBLYMETADATA*)&readMetadata, &hash, &hashLength, &flags).Code.Should().Be(0);
        new string(readName).Should().Be(assemblyName);
        Bytes(key, (uint)keyLength).Should().Equal(publicKeyToken);
        readMetadata.RevisionNumber.Should().Be(4);

        // DefineTypeRefByName deduplicates and is visible to FindTypeRef / GetTypeRefProps.
        var typeRefs = _reader.TypeReferences.Count;
        MdTypeRef first, second, found;
        fixed (char* name = "Datadog.Aot.Test.Defined")
        {
            _metadata.DefineTypeRefByName(new MdToken(assemblyRef.Value), name, &first).Code.Should().Be(0);
            _metadata.DefineTypeRefByName(new MdToken(assemblyRef.Value), name, &second).Code.Should().Be(0);
            _metadata.FindTypeRef(new MdToken(assemblyRef.Value), name, &found).Code.Should().Be(0);
        }

        first.Value.Should().Be(0x01000000 | (typeRefs + 1));
        second.Value.Should().Be(first.Value);
        found.Value.Should().Be(first.Value);

        // An existing TypeRef is returned instead of a new row.
        var existing = _reader.TypeReferences.First();
        var existingReference = _reader.GetTypeReference(existing);
        MdTypeRef existingFound;
        fixed (char* name = FullName(existingReference.Namespace, existingReference.Name))
        {
            _metadata.DefineTypeRefByName(new MdToken(MetadataTokens.GetToken(existingReference.ResolutionScope)), name, &existingFound).Code.Should().Be(0);
        }

        existingFound.Value.Should().Be(MetadataTokens.GetToken(existing));

        // Member references, signatures, type specs, method specs and user strings deduplicate and read back.
        var signature = new byte[] { 0x20, 0x00, 0x01 }; // instance void ()
        MdMemberRef memberRef, memberRefAgain;
        fixed (char* name = "Run")
        {
            fixed (byte* blob = signature)
            {
                _metadata.DefineMemberRef(new MdToken(first.Value), name, (IntPtr)blob, signature.Length, &memberRef).Code.Should().Be(0);
                _metadata.DefineMemberRef(new MdToken(first.Value), name, (IntPtr)blob, signature.Length, &memberRefAgain).Code.Should().Be(0);
            }
        }

        memberRefAgain.Value.Should().Be(memberRef.Value);
        MdToken memberParent;
        IntPtr memberSignature;
        uint memberSignatureLength, memberNameLength;
        var memberName = _buffer;
        _metadata.GetMemberRefProps(memberRef, &memberParent, memberName, NameCapacity, &memberNameLength, &memberSignature, &memberSignatureLength).Code.Should().Be(0);
        new string(memberName).Should().Be("Run");
        memberParent.Value.Should().Be(first.Value);
        Bytes(memberSignature, memberSignatureLength).Should().Equal(signature);

        var locals = new byte[] { 0x07, 0x01, 0x08 }; // locals: int32
        MdSignature localSignature, localSignatureAgain;
        fixed (byte* blob = locals)
        {
            _metadata.GetTokenFromSig((IntPtr)blob, locals.Length, &localSignature).Code.Should().Be(0);
            _metadata.GetTokenFromSig((IntPtr)blob, locals.Length, &localSignatureAgain).Code.Should().Be(0);
        }

        localSignatureAgain.Value.Should().Be(localSignature.Value);
        IntPtr readSignature;
        uint readSignatureLength;
        _metadata.GetSigFromToken(localSignature, &readSignature, &readSignatureLength).Code.Should().Be(0);
        Bytes(readSignature, readSignatureLength).Should().Equal(locals);

        var instantiation = new byte[] { 0x0A, 0x01, 0x08 }; // <int32>
        MdMethodSpec methodSpec, methodSpecAgain;
        fixed (byte* blob = instantiation)
        {
            _metadata.DefineMethodSpec(new MdToken(memberRef.Value), (IntPtr)blob, instantiation.Length, &methodSpec).Code.Should().Be(0);
            _metadata.DefineMethodSpec(new MdToken(memberRef.Value), (IntPtr)blob, instantiation.Length, &methodSpecAgain).Code.Should().Be(0);
        }

        methodSpecAgain.Value.Should().Be(methodSpec.Value);
        MdToken specParent;
        IntPtr specBlob;
        uint specLength;
        _metadata.GetMethodSpecProps(methodSpec, &specParent, &specBlob, &specLength).Code.Should().Be(0);
        specParent.Value.Should().Be(memberRef.Value);
        Bytes(specBlob, specLength).Should().Equal(instantiation);

        MdString userString, userStringAgain;
        fixed (char* value = "hello aot")
        {
            _metadata.DefineUserString(value, 9, &userString).Code.Should().Be(0);
            _metadata.DefineUserString(value, 9, &userStringAgain).Code.Should().Be(0);
        }

        userStringAgain.Value.Should().Be(userString.Value);
        var userStringBuffer = _buffer;
        uint userStringLength;
        _metadata.GetUserString(userString, userStringBuffer, 16, &userStringLength).Code.Should().Be(0);
        new string(userStringBuffer, 0, (int)userStringLength).Should().Be("hello aot");
    }

    private static List<T> Enumerate<T>(EnumFunction<T> function)
        where T : unmanaged
    {
        var result = new List<T>();
        var handle = default(HCORENUM);
        var buffer = new T[7]; // a small buffer exercises the chunked enumeration
        while (true)
        {
            uint count;
            HResult hr;
            fixed (T* pointer = buffer)
            {
                hr = function(&handle, pointer, (uint)buffer.Length, &count);
            }

            result.AddRange(buffer.Take((int)count));
            if (hr.Code != 0 || count == 0)
            {
                break;
            }
        }

        return result;
    }

    private static byte[] Bytes(IntPtr pointer, uint length)
    {
        var result = new byte[length];
        if (length > 0)
        {
            Marshal.Copy(pointer, result, 0, (int)length);
        }

        return result;
    }

    private string FullName(StringHandle @namespace, StringHandle name)
    {
        var ns = _reader.GetString(@namespace);
        var simple = _reader.GetString(name);
        return ns.Length == 0 ? simple : ns + "." + simple;
    }
}
#endif
