using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Xml.Linq;
using Datadog.Trace.Tools.AotProcessor.Interfaces;
using Mono.Cecil;
using Newtonsoft.Json.Linq;

namespace Datadog.Trace.Tools.AotProcessor.Runtime
{
    internal partial class ModuleMetadata : IMetaDataImport2, IMetaDataAssemblyImport, IMetaDataEmit2, IMetaDataAssemblyEmit, IDisposable
    {
        private NativeObjects.IMetaDataImport2 metadataImport;
        private NativeObjects.IMetaDataEmit2 metadataEmit;
        private NativeObjects.IMetaDataAssemblyImport metadataAssemblyImport;
        private NativeObjects.IMetaDataAssemblyEmit metadataAssemblyEmit;

        private nint enumId = 1;
        private Dictionary<nint, IEnumerator> enumerators = new Dictionary<nint, IEnumerator>();

        private MdToken systemRuntimeAssemblyRef = default;
        private MdToken systemRuntimeInteropServicesAssemblyRef = default;
        private MdToken systemThreadingAssemblyRef = default;

        private Dictionary<MetadataToken, GCHandle> signatures = new Dictionary<MetadataToken, GCHandle>();

        public ModuleMetadata(ModuleInfo module)
        {
            Module = module;
            metadataImport = NativeObjects.IMetaDataImport2.Wrap(this);
            metadataEmit = NativeObjects.IMetaDataEmit2.Wrap(this);
            metadataAssemblyImport = NativeObjects.IMetaDataAssemblyImport.Wrap(this);
            metadataAssemblyEmit = NativeObjects.IMetaDataAssemblyEmit.Wrap(this);
        }

        public void Dispose()
        {
            metadataImport.Dispose();
            metadataEmit.Dispose();
            metadataAssemblyImport.Dispose();
            metadataAssemblyEmit.Dispose();

            foreach (var handle in signatures.Values)
            {
                handle.Free();
            }

            signatures.Clear();
        }

        public ModuleInfo Module { get; }

        public HResult QueryInterface(in Guid guid, out IntPtr ptr)
        {
            if (
                guid == IUnknown.Guid ||
                guid == IMetaDataImport.Guid ||
                guid == IMetaDataImport2.Guid)
            {
                ptr = metadataImport;
                return HResult.S_OK;
            }
            else if (
                guid == IUnknown.Guid ||
                guid == IMetaDataEmit.Guid ||
                guid == IMetaDataEmit2.Guid)
            {
                ptr = metadataEmit;
                return HResult.S_OK;
            }
            else if (
                guid == IUnknown.Guid ||
                guid == IMetaDataAssemblyImport.Guid)
            {
                ptr = metadataAssemblyImport;
                return HResult.S_OK;
            }
            else if (
                guid == IUnknown.Guid ||
                guid == IMetaDataAssemblyEmit.Guid)
            {
                ptr = metadataAssemblyEmit;
                return HResult.S_OK;
            }

            ptr = IntPtr.Zero;
            return HResult.E_NOINTERFACE;
        }

        public int AddRef()
        {
            return 1;
        }

        public int Release()
        {
            return 1;
        }

        internal IMetadataTokenProvider? LookupToken(int tokenId)
        {
            var res = Module.Definition.LookupToken(tokenId);
            if (res is not null) { return res; }

            var token = new MetadataToken((uint)tokenId);
            if (token.TokenType == TokenType.AssemblyRef)
            {
                return Module.Definition.AssemblyReferences.FirstOrDefault(r => r.MetadataToken.ToInt32() == tokenId);
            }
            else if (token.TokenType == TokenType.ModuleRef)
            {
                return Module.Definition.ModuleReferences.FirstOrDefault(r => r.MetadataToken.ToInt32() == tokenId);
            }
            else if (token.TokenType == TokenType.Property)
            {
                foreach (var type in Module.Definition.Types)
                {
                    var property = type.Properties.FirstOrDefault(p => p.MetadataToken.ToInt32() == tokenId);
                    if (property is not null) { return property; }
                }
            }
            else if (token.TokenType == TokenType.TypeSpec)
            {
                return Module.Definition.GetTypeSpec(new MetadataToken((uint)tokenId));
            }

            return null;
        }

        internal CustomAttribute? LookupToken(MdCustomAttribute tokenId)
        {
            var token = new MetadataToken((uint)tokenId.Value);
            if (token.TokenType == TokenType.CustomAttribute)
            {
                foreach (var type in Module.Definition.Types)
                {
                    var attribute = type.CustomAttributes.FirstOrDefault(p => p.MetadataToken.ToInt32() == tokenId.Value);
                    if (attribute is not null) { return attribute; }

                    foreach (var method in type.Methods)
                    {
                        attribute = method.CustomAttributes.FirstOrDefault(p => p.MetadataToken.ToInt32() == tokenId.Value);
                        if (attribute is not null) { return attribute; }
                    }

                    foreach (var property in type.Properties)
                    {
                        attribute = property.CustomAttributes.FirstOrDefault(p => p.MetadataToken.ToInt32() == tokenId.Value);
                        if (attribute is not null) { return attribute; }
                    }
                }
            }

            return null;
        }

        internal (IntPtr Sig, uint SigSize) GetLocalSignature(MetadataToken token)
        {
            return GetSignature(token, Module.Definition.GetSignature);
        }

        internal (IntPtr Sig, uint SigSize) GetSignature(MetadataToken token, Func<MetadataToken, byte[]> ifNotFound)
        {
            if (!signatures.TryGetValue(token, out var handle))
            {
                var signature = ifNotFound(token);
                if (signature is null || signature.Length == 0)
                {
                    return (IntPtr.Zero, 0);
                }

                handle = GCHandle.Alloc(signature, GCHandleType.Pinned);
                signatures[token] = handle;
            }

            var len = ((byte[])handle.Target!).Length;

            return (handle.AddrOfPinnedObject(), (uint)len);
        }

        internal TypeReference SystemVoidRef()
        {
            var res = Module.Definition.GetTypeReferences().FirstOrDefault(r => r.Name == "Void");
            if (res is not null) { return res; }
            return new TypeReference("System", "Void", Module.Definition, null);
        }

        #region IMetaDataAssemblyEmit

        public unsafe HResult DefineAssemblyRef(IntPtr pbPublicKeyOrToken, int cbPublicKeyOrToken, char* szName, ASSEMBLYMETADATA* pMetaData, IntPtr pbHashValue, int cbHashValue, int dwAssemblyRefFlags, MdToken* pmdar)
        {
            var name = System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szName);
            if (string.IsNullOrEmpty(name)) { return HResult.E_INVALIDARG; }

            Version? version = default;
            if (pMetaData is not null)
            {
                version = new Version(pMetaData->usMajorVersion, pMetaData->usMinorVersion, pMetaData->usBuildNumber, pMetaData->usRevisionNumber);
            }

            byte[]? publicKeyToken = null;
            if (cbPublicKeyOrToken > 0)
            {
                publicKeyToken = new byte[cbPublicKeyOrToken];
                System.Runtime.InteropServices.Marshal.Copy(pbPublicKeyOrToken, publicKeyToken, 0, cbPublicKeyOrToken);
            }

            if (name == "mscorlib")
            {
                systemRuntimeAssemblyRef = DefineAssemblyRefInternal("System.Runtime", ref version, ref publicKeyToken);
                systemRuntimeInteropServicesAssemblyRef = DefineAssemblyRefInternal("System.Runtime.InteropServices", ref version, ref publicKeyToken);
                systemThreadingAssemblyRef = DefineAssemblyRefInternal("System.Threading", ref version, ref publicKeyToken);
                *pmdar = systemRuntimeAssemblyRef;
            }
            else
            {
                *pmdar = DefineAssemblyRefInternal(name, ref version, ref publicKeyToken);
            }

            return HResult.S_OK;
        }

        private MdToken DefineAssemblyRefInternal(string name, ref Version? version, ref byte[]? publicKeyToken)
        {
            var reference = Module.Definition.AssemblyReferences.FirstOrDefault(a => a.Name == name);
            if (reference is not null)
            {
                version = reference.Version;
                publicKeyToken = reference.PublicKeyToken;
                return new MdToken(reference.MetadataToken.ToInt32());
            }

            reference = new AssemblyNameReference(name, version);
            reference.PublicKeyToken = publicKeyToken;
            reference.MetadataToken = new MetadataToken(TokenType.AssemblyRef, Module.Definition.AssemblyReferences.Count + 1);

            Module.Definition.AssemblyReferences.Add(reference);
            return new MdToken(reference.MetadataToken.ToInt32());
        }

        #endregion

        #region IMetadataImport2

        public unsafe HResult FindTypeDefByName(char* szTypeDef, MdToken tkEnclosingClass, MdTypeDef* ptd)
        {
            var name = System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szTypeDef);
            var type = Module.Definition.GetType(name);
            if (type is not null && ptd is not null)
            {
                *ptd = new MdTypeDef(type.MetadataToken.ToInt32());
                return HResult.S_OK;
            }

            return HResult.E_INVALIDARG;
        }

        public void CloseEnum(HCORENUM hEnum)
        {
            if (enumerators.TryGetValue(hEnum.Value, out var enumerator))
            {
                enumerator.Dispose();
                enumerators.Remove(hEnum.Value);
            }
        }

        public unsafe HResult CountEnum(HCORENUM hEnum, uint* pulCount)
        {
            if (enumerators.TryGetValue(hEnum.Value, out var enumerator))
            {
                *pulCount = (uint)enumerator.Delivered;
                return HResult.S_OK;
            }

            *pulCount = 0;
            return HResult.E_INVALIDARG;
        }

        public HResult ResetEnum(HCORENUM hEnum, uint ulPos)
        {
            if (enumerators.TryGetValue(hEnum.Value, out var enumerator) && enumerator.Reset(ulPos))
            {
                return HResult.S_OK;
            }

            return HResult.E_INVALIDARG;
        }

        public unsafe HResult EnumTypeRefs(HCORENUM* phEnum, MdTypeRef* rTypeRefs, uint cMax, uint* pcTypeRefs)
        {
            Enumerator<TypeReference, MdTypeRef> enumerator;
            if (phEnum is null || phEnum->Value == 0)
            {
                *phEnum = new HCORENUM(enumId++);
                enumerator = new Enumerator<TypeReference, MdTypeRef>(Module.Definition.GetTypeReferences().ToArray(), (i) => new MdTypeRef(i.MetadataToken.ToInt32()));
                enumerators[phEnum->Value] = enumerator;
            }
            else
            {
                enumerator = (Enumerator<TypeReference, MdTypeRef>)enumerators[phEnum->Value];
            }

            *pcTypeRefs = enumerator.Fetch(rTypeRefs, cMax);

            return *pcTypeRefs > 0 ? HResult.S_OK : HResult.S_FALSE;
        }

        public unsafe HResult EnumTypeDefs(HCORENUM* phEnum, MdTypeDef* rTypeDefs, uint cMax, uint* pcTypeDefs)
        {
            Enumerator<TypeDefinition, MdTypeDef> enumerator;
            if (phEnum is null || phEnum->Value == 0)
            {
                *phEnum = new HCORENUM(enumId++);
                enumerator = new Enumerator<TypeDefinition, MdTypeDef>(Module.Definition.Types.ToArray(), (i) => new MdTypeDef(i.MetadataToken.ToInt32()));
                enumerators[phEnum->Value] = enumerator;
            }
            else
            {
                enumerator = (Enumerator<TypeDefinition, MdTypeDef>)enumerators[phEnum->Value];
            }

            *pcTypeDefs = enumerator.Fetch(rTypeDefs, cMax);

            return *pcTypeDefs > 0 ? HResult.S_OK : HResult.S_FALSE;
        }

        public unsafe HResult EnumMemberRefs(HCORENUM* phEnum, MdToken tkParent, MdMemberRef* rMemberRefs, uint cMax, uint* pcTokens)
        {
            var typeRef = LookupToken(tkParent.Value) as TypeReference;
            if (typeRef is null) { return HResult.E_INVALIDARG; }

            Enumerator<MemberReference, MdMemberRef> enumerator;
            if (phEnum is null || phEnum->Value == 0)
            {
                *phEnum = new HCORENUM(enumId++);
                enumerator = new Enumerator<MemberReference, MdMemberRef>(Module.Definition.GetMemberReferences().Where(e => e.DeclaringType.MetadataToken == typeRef.MetadataToken).ToArray(), (i) => new MdMemberRef(i.MetadataToken.ToInt32()));
                enumerators[phEnum->Value] = enumerator;
            }
            else
            {
                enumerator = (Enumerator<MemberReference, MdMemberRef>)enumerators[phEnum->Value];
            }

            *pcTokens = enumerator.Fetch(rMemberRefs, cMax);

            return *pcTokens > 0 ? HResult.S_OK : HResult.S_FALSE;
        }

        public unsafe HResult EnumCustomAttributes(HCORENUM* phEnum, MdToken tk, MdToken tkType, MdCustomAttribute* rCustomAttributes, uint cMax, uint* pcCustomAttributes)
        {
            var method = LookupToken(tk.Value) as MethodDefinition;
            if (method is null) { return HResult.E_INVALIDARG; }

            Enumerator<CustomAttribute, MdCustomAttribute> enumerator;
            if (phEnum is null || phEnum->Value == 0)
            {
                *phEnum = new HCORENUM(enumId++);
                enumerator = new Enumerator<CustomAttribute, MdCustomAttribute>(method.CustomAttributes.ToArray(), (i) => new MdCustomAttribute(i.MetadataToken.ToInt32()));
                enumerators[phEnum->Value] = enumerator;
            }
            else
            {
                enumerator = (Enumerator<CustomAttribute, MdCustomAttribute>)enumerators[phEnum->Value];
            }

            *pcCustomAttributes = enumerator.Fetch(rCustomAttributes, cMax);

            return *pcCustomAttributes > 0 ? HResult.S_OK : HResult.S_FALSE;
        }

        public unsafe HResult EnumProperties(HCORENUM* phEnum, MdTypeDef td, MdProperty* rProperties, uint cMax, uint* pcProperties)
        {
            var typeDef = LookupToken(td.Value) as TypeDefinition;
            if (typeDef is null) { return HResult.E_INVALIDARG; }

            Enumerator<PropertyDefinition, MdProperty> enumerator;
            if (phEnum is null || phEnum->Value == 0)
            {
                *phEnum = new HCORENUM(enumId++);
                enumerator = new Enumerator<PropertyDefinition, MdProperty>(typeDef.Properties.ToArray(), (i) => new MdProperty(i.MetadataToken.ToInt32()));
                enumerators[phEnum->Value] = enumerator;
            }
            else
            {
                enumerator = (Enumerator<PropertyDefinition, MdProperty>)enumerators[phEnum->Value];
            }

            *pcProperties = enumerator.Fetch(rProperties, cMax);

            return *pcProperties > 0 ? HResult.S_OK : HResult.S_FALSE;
        }

        public unsafe HResult EnumMethodsWithName(HCORENUM* phEnum, MdTypeDef cl, char* szName, MdMethodDef* rMethods, uint cMax, uint* pcTokens)
        {
            var typeDef = LookupToken(cl.Value) as TypeDefinition;
            if (typeDef is null) { return HResult.E_INVALIDARG; }

            var name = System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szName);
            Enumerator<MethodDefinition, MdMethodDef> enumerator;
            if (phEnum is null || phEnum->Value == 0)
            {
                *phEnum = new HCORENUM(enumId++);
                enumerator = new Enumerator<MethodDefinition, MdMethodDef>(typeDef.Methods.Where(m => m.Name == name).ToArray(), (i) => new MdMethodDef(i.MetadataToken.ToInt32()));
                enumerators[phEnum->Value] = enumerator;
            }
            else
            {
                enumerator = (Enumerator<MethodDefinition, MdMethodDef>)enumerators[phEnum->Value];
            }

            *pcTokens = enumerator.Fetch(rMethods, cMax);

            return *pcTokens > 0 ? HResult.S_OK : HResult.S_FALSE;
        }

        public unsafe HResult GetCustomAttributeProps(MdCustomAttribute cv, MdToken* ptkObj, MdToken* ptkType, IntPtr* ppBlob, uint* pcbSize)
        {
            var customAttribute = LookupToken(cv);
            if (customAttribute is null) { return HResult.E_INVALIDARG; }

            if (ptkObj is not null)
            {
                *ptkObj = new MdToken(customAttribute.Owner.MetadataToken.ToInt32());
            }

            if (ptkType is not null)
            {
                *ptkType = new MdToken(customAttribute.Constructor.MetadataToken.ToInt32());
            }

            return HResult.S_OK;
        }

        public unsafe HResult GetPropertyProps(MdProperty prop, MdTypeDef* pClass, char* szProperty, uint cchProperty, uint* pchProperty, int* pdwPropFlags, IntPtr* ppvSig, uint* pbSig, int* pdwCPlusTypeFlag, IntPtr* ppDefaultValue, uint* pcchDefaultValue, MdMethodDef* pmdSetter, MdMethodDef* pmdGetter, MdMethodDef* rmdOtherMethod, uint cMax, uint* pcOtherMethod)
        {
            var property = LookupToken(prop.Value) as PropertyDefinition;
            if (property is null) { return HResult.E_INVALIDARG; }

            if (pClass is not null)
            {
                *pClass = new MdTypeDef(property.DeclaringType.MetadataToken.ToInt32());
            }

            property.Name.CopyTo(cchProperty, szProperty, pchProperty);

            if (pdwPropFlags is not null)
            {
                *pdwPropFlags = (int)property.Attributes;
            }

            if (pdwCPlusTypeFlag is not null)
            {
                *pdwCPlusTypeFlag = 0;
            }

            if (pmdSetter is not null)
            {
                *pmdSetter = new MdMethodDef(property.SetMethod?.MetadataToken.ToInt32() ?? 0);
            }

            if (pmdGetter is not null)
            {
                *pmdGetter = new MdMethodDef(property.GetMethod?.MetadataToken.ToInt32() ?? 0);
            }

            return HResult.S_OK;
        }

        public unsafe HResult GetTypeRefProps(MdTypeRef tr, MdToken* ptkResolutionScope, char* szName, uint cchName, uint* pchName)
        {
            var typeRef = LookupToken(tr.Value) as TypeReference;
            if (typeRef is null) { return HResult.E_INVALIDARG; }

            if (ptkResolutionScope is not null)
            {
                *ptkResolutionScope = new MdToken((int)Module.Id.Value);
            }

            typeRef.FullName.CopyTo(cchName, szName, pchName);

            return HResult.S_OK;
        }

        public unsafe HResult GetMemberRefProps(MdMemberRef mr, MdToken* ptk, char* szMember, uint cchMember, uint* pchMember, IntPtr* ppvSigBlob, uint* pbSig)
        {
            return GetMemberProps(new MdToken(mr.Value), ptk, szMember, cchMember, pchMember, null, ppvSigBlob, pbSig, null, null, null, null, null);
        }

        public unsafe HResult GetMemberProps(MdToken mb, MdToken* pClass, char* szMember, uint cchMember, uint* pchMember, int* pdwAttr, IntPtr* ppvSigBlob, uint* pcbSigBlob, uint* pulCodeRVA, int* pdwImplFlags, int* pdwCPlusTypeFlag, char* ppValue, uint* pcchValue)
        {
            var member = Module.GetMember(mb.Value);
            if (member is null) { return HResult.E_INVALIDARG; }

            if (pClass is not null)
            {
                *pClass = new MdToken(member.DeclaringType.Value);
                member.Name.CopyTo(cchMember, szMember, pchMember);
            }

            if (pdwAttr is not null)
            {
                *pdwAttr = member.Attributes;
            }

            if (ppvSigBlob is not null && pcbSigBlob is not null)
            {
                *ppvSigBlob = member.GetSignature();
                *pcbSigBlob = member.SignatureLength;
            }

            if (pdwImplFlags is not null)
            {
                *pdwImplFlags = 0;
            }

            if (pcchValue is not null)
            {
                *pcchValue = 0;
            }

            return HResult.S_OK;
        }

        public unsafe HResult GetMethodSpecProps(MdMethodSpec mi, MdToken* tkParent, IntPtr* ppvSigBlob, uint* pcbSigBlob)
        {
            var methodSpec = Module.GetMember(mi.Value) as MethodSpecInfo;
            if (methodSpec is null) { return HResult.E_INVALIDARG; }

            if (tkParent is not null)
            {
                *tkParent = new MdToken(methodSpec.Definition.ElementMethod.MetadataToken.ToInt32());
            }

            if (ppvSigBlob is not null && pcbSigBlob is not null)
            {
                *ppvSigBlob = methodSpec.GetSignature();
                *pcbSigBlob = methodSpec.SignatureLength;
            }

            return HResult.S_OK;
        }

        public unsafe HResult GetTypeDefProps(MdTypeDef td, char* szTypeDef, uint cchTypeDef, uint* pchTypeDef, int* pdwTypeDefFlags, MdToken* ptkExtends)
        {
            var type = LookupToken(td.Value) as TypeDefinition;
            if (type is null) { return HResult.E_INVALIDARG; }

            type.Name.CopyTo(cchTypeDef, szTypeDef, pchTypeDef);

            if (pdwTypeDefFlags is not null)
            {
                *pdwTypeDefFlags = (int)type.Attributes;
            }

            if (ptkExtends is not null)
            {
                *ptkExtends = new MdToken(type.BaseType?.MetadataToken.ToInt32() ?? 0);
            }

            return HResult.S_OK;
        }

        public unsafe HResult GetNestedClassProps(MdTypeDef tdNestedClass, MdTypeDef* ptdEnclosingClass)
        {
            var type = LookupToken(tdNestedClass.Value) as TypeDefinition;
            if (type is null) { return HResult.E_INVALIDARG; }

            *ptdEnclosingClass = type.IsNested ? new MdTypeDef(type.DeclaringType.MetadataToken.ToInt32()) : default;
            return HResult.S_OK;
        }

        public unsafe HResult GetMethodProps(MdMethodDef mb, MdToken* pClass, char* szMethod, uint cchMethod, uint* pchMethod, int* pdwAttr, IntPtr* ppvSigBlob, uint* pcbSigBlob, uint* pulCodeRVA, int* pdwImplFlags)
        {
            return GetMemberProps(new MdToken(mb.Value), pClass, szMethod, cchMethod, pchMethod, pdwAttr, ppvSigBlob, pcbSigBlob, pulCodeRVA, pdwImplFlags, null, null, null);
        }

        public unsafe HResult GetUserString(MdString stk, char* szString, uint cchString, uint* pchString)
        {
            var userString = Module.Definition.GetUserString(new MetadataToken((uint)stk.Value));
            if (userString is null) { return HResult.E_INVALIDARG; }

            userString.CopyTo(cchString, szString, pchString);
            return HResult.S_OK;
        }

        public unsafe HResult FindMethod(MdTypeDef td, char* szName, nint* pvSigBlob, uint cbSigBlob, MdMethodDef* pmb)
        {
            var type = LookupToken(td.Value) as TypeDefinition;
            if (type is null) { return HResult.E_INVALIDARG; }

            var name = System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szName);
            var method = type.Methods.FirstOrDefault(m => m.Name == name);
            if (method is null) { return HResult.E_INVALIDARG; }

            *pmb = new MdMethodDef(method.MetadataToken.ToInt32());

            return HResult.S_OK;
        }

        public unsafe HResult GetModuleFromScope(MdModule* pmd)
        {
            *pmd = new MdModule((int)Module.Id.Value);
            return HResult.S_OK;
        }

        public unsafe HResult GetSigFromToken(MdSignature mdSig, IntPtr* ppvSig, uint* pcbSig)
        {
            var signature = GetLocalSignature(new MetadataToken((uint)mdSig.Value));
            if (signature.SigSize == 0)
            {
                ppvSig = null;
                *pcbSig = 0;
                return HResult.E_INVALIDARG;
            }

            *ppvSig = signature.Sig;
            *pcbSig = signature.SigSize;

            return HResult.S_OK;
        }

        public unsafe HResult GetTypeSpecFromToken(MdTypeSpec typespec, IntPtr* ppvSig, uint* pcbSig)
        {
            var type = Module.Definition.GetTypeSpec(new MetadataToken((uint)typespec.Value));
            if (type is null) { return HResult.E_RECORD_NOT_FOUND; }

            var signature = GetSignature(type.MetadataToken, (_) => type.RawSignature);
            if (signature.SigSize == 0)
            {
                ppvSig = null;
                *pcbSig = 0;
                return HResult.E_INVALIDARG;
            }

            *ppvSig = signature.Sig;
            *pcbSig = signature.SigSize;

            return HResult.S_OK;
        }

        #endregion

        #region IMetadataEmit

        public unsafe HResult DefineTypeRefByName(MdToken tkResolutionScope, char* szName, MdTypeRef* ptr)
        {
            var name = System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szName);
            if (string.IsNullOrEmpty(name)) { return HResult.E_INVALIDARG; }
            if (tkResolutionScope.Value == systemRuntimeAssemblyRef.Value)
            {
                if (name.StartsWith("System.Threading."))
                {
                    tkResolutionScope = (MdToken)systemThreadingAssemblyRef;
                }
                else if (name.StartsWith("System.Runtime.InteropServices."))
                {
                    tkResolutionScope = (MdToken)systemRuntimeInteropServicesAssemblyRef;
                }
            }

            var scope = LookupToken(tkResolutionScope.Value) as IMetadataScope;

            // Look for existing
            var existing = Module.Definition.GetTypeReferences().FirstOrDefault(r => r.FullName == name && (r.Scope == null || r.Scope.MetadataToken.ToInt32() == tkResolutionScope.Value));
            if (existing is not null)
            {
                *ptr = new MdTypeRef(existing.MetadataToken.ToInt32());
                return HResult.S_OK;
            }

            // Split name
            SplitTypeName(name, out var @namespace, out var typeName);

            var typeRef = Module.Definition.AddRaw(new TypeReference(@namespace, typeName, Module.Definition, scope));
            *ptr = new MdTypeRef(typeRef.MetadataToken.ToInt32());

            return HResult.S_OK;
        }

        public unsafe HResult DefineMemberRef(MdToken tkImport, char* szName, IntPtr pvSigBlob, int cbSigBlob, MdMemberRef* pmr)
        {
            // We are going to suppose it's a method reference by now
            var typeRef = LookupToken(tkImport.Value) as TypeReference;
            var name = System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szName);

            // Look for existing
            var existing = Module.Definition.GetMemberReferences().FirstOrDefault(r => r.Name == name && r.DeclaringType.MetadataToken.ToInt32() == tkImport.Value);
            if (existing is not null)
            {
                *pmr = new MdMemberRef(existing.MetadataToken.ToInt32());
                return HResult.S_OK;
            }

            var sig = new byte[cbSigBlob];
            System.Runtime.InteropServices.Marshal.Copy(pvSigBlob, sig, 0, cbSigBlob);
            var methodRef = Module.Definition.AddRaw(new MethodReference(name, sig, typeRef));
            *pmr = new MdMemberRef(methodRef.MetadataToken.ToInt32());
            return HResult.S_OK;
        }

        public unsafe HResult DefineUserString(char* szString, int cchString, MdString* pstk)
        {
            var userString = System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szString);
            *pstk = new MdString(Module.Definition.AddRaw(userString).ToInt32());
            return HResult.S_OK;
        }

        public unsafe HResult DefineTypeDef(char* szTypeDef, int dwTypeDefFlags, MdToken tkExtends, MdToken* rtkImplements, MdTypeDef* ptd)
        {
            var name = System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szTypeDef);
            if (string.IsNullOrEmpty(name)) { return HResult.E_INVALIDARG; }

            var baseType = LookupToken(tkExtends.Value) as TypeReference;
            SplitTypeName(name, out var @namespace, out var typeName);

            var type = Module.Definition.AddRaw(new TypeDefinition(@namespace, typeName, (TypeAttributes)dwTypeDefFlags, baseType));
            *ptd = new MdTypeDef(type.MetadataToken.ToInt32());

            return HResult.S_OK;
        }

        public unsafe HResult DefineField(MdTypeDef td, char* szName, int dwFieldFlags, IntPtr pvSigBlob, int cbSigBlob, int dwCPlusTypeFlag, IntPtr pValue, int cchValue, MdFieldDef* pmd)
        {
            var type = LookupToken(td.Value) as TypeDefinition;
            var name = System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szName);
            var sig = new byte[cbSigBlob];
            System.Runtime.InteropServices.Marshal.Copy(pvSigBlob, sig, 0, cbSigBlob);

            var field = new FieldDefinition(name, (FieldAttributes)dwFieldFlags, sig, type);
            Module.Definition.AddRaw(field);
            *pmd = new MdFieldDef(field.MetadataToken.ToInt32());

            return HResult.S_OK;
        }

        public unsafe HResult DefineMethod(MdTypeDef td, char* szName, int dwMethodFlags, IntPtr pvSigBlob, int cbSigBlob, int ulCodeRVA, int dwImplFlags, MdMethodDef* pmd)
        {
            var type = LookupToken(td.Value) as TypeDefinition;
            var name = System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szName);
            var sig = new byte[cbSigBlob];
            System.Runtime.InteropServices.Marshal.Copy(pvSigBlob, sig, 0, cbSigBlob);

            var method = Module.Definition.AddRaw(new MethodDefinition(name, (MethodAttributes)dwMethodFlags, sig, type));
            *pmd = new MdMethodDef(method.MetadataToken.ToInt32());

            return HResult.S_OK;
        }

        public HResult SetMethodImplFlags(MdMethodDef md, int dwImplFlags)
        {
            var method = LookupToken(md.Value) as MethodDefinition;
            if (method is null) { return HResult.E_INVALIDARG; }

            method.ImplAttributes = (MethodImplAttributes)dwImplFlags;
            return HResult.S_OK;
        }

        public unsafe HResult DefineModuleRef(char* szName, MdModuleRef* pmur)
        {
            var name = System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szName);
            if (string.IsNullOrEmpty(name)) { return HResult.E_INVALIDARG; }
            var fileName = Path.GetFileName(name);

            var module = Module.Definition.AddRaw(new ModuleReference(fileName));
            *pmur = new MdModuleRef(module.MetadataToken.ToInt32());
            return HResult.S_OK;
        }

        public unsafe HResult DefinePinvokeMap(MdToken tk, int dwMappingFlags, char* szImportName, MdModuleRef mrImportDLL)
        {
            var methodDef = LookupToken(tk.Value) as MethodDefinition;
            var moduleRef = LookupToken(mrImportDLL.Value) as ModuleReference;
            if (methodDef is null || moduleRef is null) { return HResult.E_INVALIDARG; }

            methodDef.PInvokeInfo = new PInvokeInfo((PInvokeAttributes)dwMappingFlags, System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szImportName), moduleRef);

            return HResult.S_OK;
        }

        public unsafe HResult GetTokenFromSig(IntPtr pvSig, int cbSig, MdSignature* pmsig)
        {
            var sig = new byte[cbSig];
            System.Runtime.InteropServices.Marshal.Copy(pvSig, sig, 0, cbSig);

            *pmsig = new MdSignature(Module.Definition.AddRaw(sig).ToInt32());

            return HResult.S_OK;
        }

        public unsafe HResult DefineImportMember(IntPtr pAssemImport, byte* pbHashValue, int cbHashValue, IntPtr pImport, MdToken mbMember, IntPtr pAssemEmit, MdToken tkParent, MdMemberRef* pmr)
        {
            var metadataAssemblyImport = new NativeObjects.IMetaDataAssemblyImportInvoker(pAssemImport);
            var metadataImport = new NativeObjects.IMetaDataImportInvoker(pImport);
            var metadataAssemblyEmit = new NativeObjects.IMetaDataAssemblyEmitInvoker(pAssemEmit);

            MdToken type;
            char[] name = new char[256];
            uint nameLength;
            int attr;
            IntPtr pbSigBlob;
            uint pnSigBlob;

            fixed (char* pName = name)
            {
                var hr = metadataImport.GetMemberProps(mbMember, &type, pName, 256, &nameLength, &attr, &pbSigBlob, &pnSigBlob, null, null, null, null, null);
                if (hr.Failed) { return hr; }

                hr = DefineMemberRef(tkParent, pName, pbSigBlob, (int)pnSigBlob, pmr);

                return hr;
            }
        }

        public unsafe HResult GetTokenFromTypeSpec(IntPtr pvSig, int cbSig, MdTypeSpec* ptypespec)
        {
            byte[] sig = new byte[cbSig];
            System.Runtime.InteropServices.Marshal.Copy(pvSig, sig, 0, cbSig);

            var typeSpec = Module.Definition.AddRawTypeSpec(sig);
            *ptypespec = new MdTypeSpec(typeSpec.MetadataToken.ToInt32());

            return HResult.S_OK;
        }

        public unsafe HResult DefineMethodSpec(MdToken tkParent, IntPtr pvSigBlob, int cbSigBlob, MdMethodSpec* pmi)
        {
            var parent = LookupToken(tkParent.Value) as MethodReference;
            if (parent is null) { return HResult.E_INVALIDARG; }

            byte[] sig = new byte[cbSigBlob];
            System.Runtime.InteropServices.Marshal.Copy(pvSigBlob, sig, 0, cbSigBlob);
            var method = Module.Definition.AddRawMethodSpec(parent, sig);
            *pmi = new MdMethodSpec(method.MetadataToken.ToInt32());

            return HResult.S_OK;
        }

        #endregion

        #region IMetadataAssemblyImport

        public unsafe HResult GetAssemblyFromScope(out MdAssembly ptkAssembly)
        {
            ptkAssembly = new MdAssembly((int)Module.Assembly.Id.Value);
            return HResult.S_OK;
        }

        public unsafe HResult GetAssemblyProps(MdAssembly mda, IntPtr* ppbPublicKey, int* pcbPublicKey, int* pulHashAlgId, char* szName, uint cchName, uint* pchName, ASSEMBLYMETADATA* pMetaData, int* pdwAssemblyFlags)
        {
            var assembly = Module.Assembly.Runtime.GetAssemblyInfo(mda.Value);
            if (assembly is null)
            {
                return HResult.E_INVALIDARG;
            }

            if (assembly.Definition.Name.HasPublicKey)
            {
                // Retrieve the public key (not needed ATM)
            }

            assembly.Name.CopyTo(cchName, szName, pchName);

            *pMetaData = assembly.AssemblyMetaData;

            return HResult.S_OK;
        }

        public unsafe HResult EnumAssemblyRefs(HCORENUM* phEnum, MdAssemblyRef* rAssemblyRefs, uint cMax, out uint pcTokens)
        {
            Enumerator<AssemblyNameReference, MdAssemblyRef> enumerator;
            if (phEnum is null || phEnum->Value == 0)
            {
                *phEnum = new HCORENUM(enumId++);
                enumerator = new Enumerator<AssemblyNameReference, MdAssemblyRef>(Module.Definition.AssemblyReferences.ToArray(), (i) => new MdAssemblyRef(i.MetadataToken.ToInt32()));
                enumerators[phEnum->Value] = enumerator;
            }
            else
            {
                enumerator = (Enumerator<AssemblyNameReference, MdAssemblyRef>)enumerators[phEnum->Value];
            }

            pcTokens = enumerator.Fetch(rAssemblyRefs, cMax);

            return HResult.S_OK;
        }

        public unsafe HResult GetAssemblyRefProps(MdAssemblyRef mdar, byte* ppbPublicKeyOrToken, int* pcbPublicKeyOrToken, char* szName, uint cchName, uint* pchName, ASSEMBLYMETADATA* pMetaData, byte* ppbHashValue, int* pcbHashValue, int* pdwAssemblyRefFlags)
        {
            var assemblyRef = Module.Definition.AssemblyReferences.FirstOrDefault(a => a.MetadataToken.ToInt32() == mdar.Value);
            if (assemblyRef is null) { return HResult.E_INVALIDARG; }

            assemblyRef.Name.CopyTo(cchName, szName, pchName);
            if (pMetaData is not null)
            {
                *pMetaData = new ASSEMBLYMETADATA(assemblyRef.Version.Major, assemblyRef.Version.Minor, assemblyRef.Version.Build, assemblyRef.Version.Revision);
            }

            return HResult.S_OK;
        }

        public unsafe HResult FindTypeRef(MdToken tkResolutionScope, char* szName, MdTypeRef* ptr)
        {
            var name = System.Runtime.InteropServices.Marshal.PtrToStringAuto((IntPtr)szName);
            if (string.IsNullOrEmpty(name)) { return HResult.E_INVALIDARG; }

            var typeRef = Module.Definition.GetTypeReferences().FirstOrDefault(t => t.FullName == name);
            if (typeRef is null) { return HResult.E_RECORD_NOT_FOUND; }

            *ptr = new MdTypeRef(typeRef.MetadataToken.ToInt32());
            return HResult.S_OK;
        }

        #endregion

        private void SplitTypeName(string name, out string @namespace, out string typeName)
        {
            var index = name.LastIndexOf('.');
            if (index == -1)
            {
                @namespace = string.Empty;
                typeName = name;
            }
            else
            {
                @namespace = name.Substring(0, index);
                typeName = name.Substring(index + 1);
            }
        }
    }
}
