#pragma once
#include "../../../../shared/src/native-src/pal.h"
#include "iast_util.h"
#include "app_domain_info.h"

using namespace shared;

namespace iast
{
    class TypeInfo;
    class MethodInfo;
    class ILRewriter;
    class ILRewriter;
    class Dataflow;
    class SignatureInfo;
    class MemberRefInfo;
    class MethodSpec;
    class MethodInfo;
    class FieldInfo;
    class PropertyInfo;
    class DataflowAspect;
    class DataflowAspectReference;
    class AspectFilter;
    enum class DataflowAspectFilterValue;

    static COR_SIGNATURE voidSig[] = { IMAGE_CEE_CS_CALLCONV_DEFAULT, 0x00, ELEMENT_TYPE_VOID };
    static COR_SIGNATURE aspectSig[] = { IMAGE_CEE_CS_CALLCONV_DEFAULT, 0x01, ELEMENT_TYPE_OBJECT, ELEMENT_TYPE_OBJECT };

    class ModuleInfo
    {
        friend class Dataflow;
        friend class ILRewriter;
        friend class TypeInfo;
        friend class MemberInfo;
        friend class MemberRefInfo;
        friend class MethodSpec;
        friend class MethodInfo;
        friend class FieldInfo;
        friend class PropertyInfo;
        friend class DataflowAspectClass;
        friend class DataflowAspect;
        friend class DataflowAspectReference;
        friend class SignatureInfo;
    private:
        struct AssemblyImportInfo
        {
            ModuleID moduleId;
            mdAssemblyRef assemblyRef;
        };
        std::unordered_map<WSTRING, AssemblyImportInfo> _mAssemblyImports;
        std::unordered_map<WSTRING, mdMemberRef> _mMemberImports;
       
        std::unordered_map<mdTypeDef, TypeInfo*> _types;
        std::unordered_map<mdMemberRef, MemberRefInfo*> _members;
        std::unordered_map<mdMethodDef, MethodInfo*> _methods;
        std::unordered_map<mdMethodDef, FieldInfo*> _fields;
        std::unordered_map<mdProperty, PropertyInfo*> _properties;
        std::unordered_map<mdMethodSpec, MethodSpec*> _specs;
        std::unordered_map<mdSignature, SignatureInfo*> _signatures;


    protected:
        CS _cs;
        Dataflow* _dataflow = nullptr;
        IMetaDataImport2* _metadataImport = nullptr;
        IMetaDataEmit2* _metadataEmit = nullptr;
        IMetaDataAssemblyImport* _assemblyImport = nullptr;
        IMetaDataAssemblyEmit* _assemblyEmit = nullptr;

        bool _isExcluded = false;


        HRESULT GetTypeDef(const WSTRING& typeName, mdTypeDef* pTypeDef);
        HRESULT GetMethodDef(mdTypeDef typeDef, const WSTRING& methodName, PCCOR_SIGNATURE pSignature, ULONG nSignature, mdMethodDef* pMethodDef);

        HRESULT GetAssemblyRef(const WSTRING& assemblyName, mdAssemblyRef* assemblyRef, bool create = true);
        HRESULT GetAssemblyRef(const WSTRING& assemblyName, const ASSEMBLYMETADATA* assemblyMetadata, const void* pPublicKeyToken, UINT nPublicKeyToken, mdAssemblyRef* assemblyRef, bool create = true);
        HRESULT GetSystemCoreAssemblyRef(mdAssemblyRef* assemblyRef);
        HRESULT GetMscorlibAssemblyRef(mdAssemblyRef* assemblyRef);
        HRESULT GetTypeRef(mdToken tkResolutionScope, const WSTRING& name, mdTypeRef* typeRef, bool create = true);
        HRESULT GetMemberRefInfo(mdTypeRef typeRef, const WSTRING& memberName, PCCOR_SIGNATURE pSignature, ULONG nSignature, mdMemberRef* memberRef, bool create = true);

        HRESULT FindTypeRefByName(const WSTRING& name, mdTypeRef* typeRef, mdToken* tkResolutionScope = nullptr);
        
        mdString DefineUserString(const WSTRING& string);

        std::vector<MethodInfo*> GetMethods(mdTypeDef typeDef);

        static bool IsNestedType(DWORD typeDefFlags);
        WSTRING GetAssemblyName();
        WSTRING GetTypeName(mdTypeDef typeDef);

        virtual HRESULT InstrumentModule_Internal() { return S_FALSE; }

        ModuleInfo* GetModuleInfoByName(WSTRING moduleName);

    public:
        ModuleID                        _id = 0;
        WSTRING                         _path = EmptyWStr;
        AssemblyID                      _assemblyId = 0;
        WSTRING                         _name = EmptyWStr;
        AppDomainInfo                   _appDomain;

        ModuleInfo(Dataflow* pDataflow, AppDomainInfo* pAppDomain, ModuleID moduleId, const WSTRING& path, AssemblyID assemblyId, const WSTRING& name, bool isGeneric = false);
        virtual ~ModuleInfo();

        Dataflow* GetDataflow();
        IMetaDataImport2* GetMetaDataImport();

        bool IsValid();
        bool IsExcluded();
        virtual bool IsCoreLib();
        WSTRING GetModuleFullName();
        mdToken DefineMemberRef(const WSTRING& moduleName, const WSTRING& typeName, const WSTRING& methodName, const WSTRING& methodParams);
        mdMethodSpec DefineMethodSpec(mdMemberRef targetMethod, SignatureInfo* sig);

        virtual bool IsInlineEnabled();

        TypeInfo* GetTypeInfo(mdTypeDef typeDef);
        MemberRefInfo* GetMemberRefInfo(mdMemberRef token);
        MethodInfo* GetMethodInfo(mdMethodDef methodDef);
        FieldInfo* GetFieldInfo(mdFieldDef fieldDef);
        PropertyInfo* GetPropertyInfo(mdProperty propId);
        MethodSpec* GetMethodSpec(mdMethodSpec methodSpec);
        SignatureInfo* GetSignature(mdSignature sigToken);
        WSTRING GetUserString(mdString token);

        std::vector<mdMethodDef> GetTypeMethodDefs(mdTypeDef typeDef);
        std::vector<MethodInfo*> GetMethods(mdTypeDef typeDef, const WSTRING& name);
        std::vector<MethodInfo*> GetMethods(mdTypeDef typeDef, const WSTRING& methodName, ULONG paramCount);
        std::vector<MethodInfo*> GetMethods(const WSTRING& typeName, const WSTRING& methodName);

        std::vector<PropertyInfo*> GetProperties(mdTypeDef typeDef);

        MethodInfo* GetMethod(const WSTRING& typeName, const WSTRING& methodName, PCCOR_SIGNATURE pSignature, ULONG nSignature);
        MethodInfo* GetMethod(mdTypeDef typeDef, const WSTRING& methodName, PCCOR_SIGNATURE pSignature, ULONG nSignature);
        MethodInfo* GetMethod(const WSTRING& typeName, const WSTRING& methodName, int paramCount = 0);
        MethodInfo* GetMethod(mdTypeDef typeDef, const WSTRING& methodName, int paramCount = 0);
        MethodInfo* GetMethod(const WSTRING& typeName, const WSTRING& methodName, const WSTRING& methodParams);
        MethodInfo* GetMethod(mdTypeDef typeDef, const WSTRING& methodName, const WSTRING& methodParams);

        static HRESULT GetILRewriter(MethodInfo* methodInfo, ILRewriter** rewriter);
        HRESULT GetILRewriter(const WSTRING& typeName, const WSTRING& methodName, int requiredParamCount, ILRewriter** rewriter);
        HRESULT GetILRewriter(const WSTRING& typeName, const WSTRING& methodName, PCCOR_SIGNATURE pSignature, ULONG nSignature, ILRewriter** rewriter, MethodInfo** pMethodInfo = nullptr);
        static HRESULT CommitILRewriter(ILRewriter** rewriter);

        bool AreSameTypes(mdTypeRef typeRef1, mdTypeRef typeRef2);

        HRESULT FindMemberRefsByName(mdTypeRef typeRef, const WSTRING& memberName, std::vector<mdMemberRef>& members);
        HRESULT GetAssemblyTypeRef(const WSTRING& assemblyName, const WSTRING& typeName, mdTypeRef* typeRef);

        std::vector<WSTRING> GetCustomAttributes(mdToken token);
    };
}