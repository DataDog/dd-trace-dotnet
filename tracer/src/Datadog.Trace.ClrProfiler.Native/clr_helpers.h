#ifndef DD_CLR_PROFILER_CLR_HELPERS_H_
#define DD_CLR_PROFILER_CLR_HELPERS_H_

#include <corhlpr.h>
#include <corprof.h>
#include <functional>
#include <utility>

#include "integration.h"

#include "../../../shared/src/native-src/util.h"
#include "../../../shared/src/native-src/com_ptr.h"

#include <set>

namespace trace
{
class ModuleMetadata;

const size_t kNameMaxSize = 1024;
const ULONG kEnumeratorMax = 256;

const auto SystemBoolean = WStr("System.Boolean");
const auto SystemChar = WStr("System.Char");
const auto SystemByte = WStr("System.Byte");
const auto SystemSByte = WStr("System.SByte");
const auto SystemUInt16 = WStr("System.UInt16");
const auto SystemInt16 = WStr("System.Int16");
const auto SystemInt32 = WStr("System.Int32");
const auto SystemUInt32 = WStr("System.UInt32");
const auto SystemInt64 = WStr("System.Int64");
const auto SystemUInt64 = WStr("System.UInt64");
const auto SystemSingle = WStr("System.Single");
const auto SystemDouble = WStr("System.Double");
const auto SystemIntPtr = WStr("System.IntPtr");
const auto SystemUIntPtr = WStr("System.UIntPtr");
const auto SystemString = WStr("System.String");
const auto SystemObject = WStr("System.Object");
const auto SystemException = WStr("System.Exception");
const auto SystemTypeName = WStr("System.Type");
const auto GetTypeFromHandleMethodName = WStr("GetTypeFromHandle");
const auto RuntimeTypeHandleTypeName = WStr("System.RuntimeTypeHandle");
const auto RuntimeMethodHandleTypeName = WStr("System.RuntimeMethodHandle");

template <typename T>
class EnumeratorIterator;

template <typename T>
class Enumerator
{
private:
    const std::function<HRESULT(HCORENUM*, T[], ULONG, ULONG*)> callback_;
    const std::function<void(HCORENUM)> close_;
    mutable HCORENUM ptr_;

public:
    Enumerator(std::function<HRESULT(HCORENUM*, T[], ULONG, ULONG*)> callback, std::function<void(HCORENUM)> close) :
        callback_(callback), close_(close), ptr_(nullptr)
    {
    }

    Enumerator(const Enumerator& other) = default;

    Enumerator& operator=(const Enumerator& other) = default;

    ~Enumerator()
    {
        close_(ptr_);
    }

    EnumeratorIterator<T> begin() const
    {
        return EnumeratorIterator<T>(this, S_OK);
    }

    EnumeratorIterator<T> end() const
    {
        return EnumeratorIterator<T>(this, S_FALSE);
    }

    HRESULT Next(T arr[], ULONG max, ULONG* cnt) const
    {
        return callback_(&ptr_, arr, max, cnt);
    }
};

template <typename T>
class EnumeratorIterator
{
private:
    const Enumerator<T>* enumerator_;
    HRESULT status_ = S_FALSE;
    T arr_[kEnumeratorMax]{};
    ULONG idx_ = 0;
    ULONG sz_ = 0;

public:
    EnumeratorIterator(const Enumerator<T>* enumerator, HRESULT status) : enumerator_(enumerator)
    {
        if (status == S_OK)
        {
            status_ = enumerator_->Next(arr_, kEnumeratorMax, &sz_);
            if (status_ == S_OK && sz_ == 0)
            {
                status_ = S_FALSE;
            }
        }
        else
        {
            status_ = status;
        }
    }

    bool operator!=(EnumeratorIterator const& other) const
    {
        return enumerator_ != other.enumerator_ || (status_ == S_OK) != (other.status_ == S_OK);
    }

    T const& operator*() const
    {
        return arr_[idx_];
    }

    EnumeratorIterator<T>& operator++()
    {
        if (idx_ < sz_ - 1)
        {
            idx_++;
        }
        else
        {
            idx_ = 0;
            status_ = enumerator_->Next(arr_, kEnumeratorMax, &sz_);
            if (status_ == S_OK && sz_ == 0)
            {
                status_ = S_FALSE;
            }
        }
        return *this;
    }
};

static Enumerator<mdTypeDef> EnumTypeDefs(const ComPtr<IMetaDataImport2>& metadata_import)
{
    return Enumerator<mdTypeDef>(
        [metadata_import](HCORENUM* ptr, mdTypeDef arr[], ULONG max, ULONG* cnt) -> HRESULT {
            return metadata_import->EnumTypeDefs(ptr, arr, max, cnt);
        },
        [metadata_import](HCORENUM ptr) -> void { metadata_import->CloseEnum(ptr); });
}

static Enumerator<mdTypeRef> EnumTypeRefs(const ComPtr<IMetaDataImport2>& metadata_import)
{
    return Enumerator<mdTypeRef>(
        [metadata_import](HCORENUM* ptr, mdTypeRef arr[], ULONG max, ULONG* cnt) -> HRESULT {
            return metadata_import->EnumTypeRefs(ptr, arr, max, cnt);
        },
        [metadata_import](HCORENUM ptr) -> void { metadata_import->CloseEnum(ptr); });
}

static Enumerator<mdModuleRef> EnumModuleRefs(const ComPtr<IMetaDataImport2>& metadata_import)
{
    return Enumerator<mdModuleRef>(
        [metadata_import](HCORENUM* ptr, mdModuleRef arr[], ULONG max, ULONG* cnt) -> HRESULT {
            return metadata_import->EnumModuleRefs(ptr, arr, max, cnt);
        },
        [metadata_import](HCORENUM ptr) -> void { metadata_import->CloseEnum(ptr); });
}

static Enumerator<mdAssemblyRef> EnumAssemblyRefs(const ComPtr<IMetaDataAssemblyImport>& assembly_import)
{
    return Enumerator<mdAssemblyRef>(
        [assembly_import](HCORENUM* ptr, mdAssemblyRef arr[], ULONG max, ULONG* cnt) -> HRESULT {
            return assembly_import->EnumAssemblyRefs(ptr, arr, max, cnt);
        },
        [assembly_import](HCORENUM ptr) -> void { assembly_import->CloseEnum(ptr); });
}

struct RuntimeInformation
{
    COR_PRF_RUNTIME_TYPE runtime_type;
    USHORT major_version;
    USHORT minor_version;
    USHORT build_version;
    USHORT qfe_version;

    RuntimeInformation() :
        runtime_type((COR_PRF_RUNTIME_TYPE) 0x0), major_version(0), minor_version(0), build_version(0), qfe_version(0)
    {
    }

    RuntimeInformation(COR_PRF_RUNTIME_TYPE runtime_type, USHORT major_version, USHORT minor_version,
                       USHORT build_version, USHORT qfe_version) :
        runtime_type(runtime_type),
        major_version(major_version),
        minor_version(minor_version),
        build_version(build_version),
        qfe_version(qfe_version)
    {
    }

    RuntimeInformation& operator=(const RuntimeInformation& other)
    {
        runtime_type = other.runtime_type;
        major_version = other.major_version;
        minor_version = other.minor_version;
        build_version = other.build_version;
        qfe_version = other.qfe_version;
        return *this;
    }

    bool is_desktop() const
    {
        return runtime_type == COR_PRF_DESKTOP_CLR;
    }
    bool is_core() const
    {
        return runtime_type == COR_PRF_CORE_CLR;
    }
};

struct AssemblyInfo
{
    const AssemblyID id;
    const shared::WSTRING name;
    const ModuleID manifest_module_id;
    const AppDomainID app_domain_id;
    const shared::WSTRING app_domain_name;

    AssemblyInfo() : id(0), name(shared::EmptyWStr), manifest_module_id(0), app_domain_id(0), app_domain_name(shared::EmptyWStr)
    {
    }

    AssemblyInfo(AssemblyID id, shared::WSTRING name, ModuleID manifest_module_id, AppDomainID app_domain_id,
                 shared::WSTRING app_domain_name) :
        id(id),
        name(name),
        manifest_module_id(manifest_module_id),
        app_domain_id(app_domain_id),
        app_domain_name(app_domain_name)
    {
    }

    bool IsValid() const
    {
        return id != 0;
    }
};

struct AssemblyMetadata
{
    const ModuleID module_id;
    const shared::WSTRING name;
    const mdAssembly assembly_token;
    const Version version;

    AssemblyMetadata() : module_id(0), name(shared::EmptyWStr), assembly_token(mdTokenNil)
    {
    }

    AssemblyMetadata(ModuleID module_id, shared::WSTRING name, mdAssembly assembly_token, USHORT major, USHORT minor,
                     USHORT build, USHORT revision) :
        module_id(module_id),
        name(name),
        assembly_token(assembly_token),
        version(Version(major, minor, build, revision))
    {
    }

    AssemblyMetadata(const AssemblyMetadata& value) :
        module_id(value.module_id), name(value.name), assembly_token(value.assembly_token), version(value.version)
    {
    }

    bool IsValid() const
    {
        return module_id != 0;
    }
};

struct AssemblyProperty
{
    const void* ppbPublicKey;
    ULONG pcbPublicKey;
    ULONG pulHashAlgId;
    ASSEMBLYMETADATA pMetaData{};
    shared::WSTRING szName;
    DWORD assemblyFlags = 0;

    AssemblyProperty() : ppbPublicKey(nullptr), pcbPublicKey(0), pulHashAlgId(0), szName(shared::EmptyWStr)
    {
    }

    AssemblyProperty(shared::WSTRING szName, void* ppbPublicKey, ULONG pcbPublicKey, ULONG pulHashAlgId, DWORD assemblyFlags) :
        szName(szName),
        ppbPublicKey(ppbPublicKey),
        pcbPublicKey(pcbPublicKey),
        pulHashAlgId(pulHashAlgId),
        assemblyFlags(assemblyFlags)
    {
    }

public:
    AssemblyProperty WithVersion(USHORT usMajorVersion, USHORT usMinorVersion, USHORT usBuildNumber, USHORT usRevisionNumber)
    {
        pMetaData.usMajorVersion = usMajorVersion;
        pMetaData.usMinorVersion = usMinorVersion;
        pMetaData.usBuildNumber = usBuildNumber;
        pMetaData.usRevisionNumber = usRevisionNumber;

        return *this;
    }
};

struct ModuleInfo
{
    const ModuleID id;
    const shared::WSTRING path;
    const AssemblyInfo assembly;
    const DWORD flags;

    ModuleInfo() : id(0), path(shared::EmptyWStr), assembly({}), flags(0)
    {
    }
    ModuleInfo(ModuleID id, shared::WSTRING path, AssemblyInfo assembly, DWORD flags) :
        id(id), path(path), assembly(assembly), flags(flags)
    {
    }

    bool IsValid() const
    {
        return id != 0;
    }

    bool IsWindowsRuntime() const
    {
        return ((flags & COR_PRF_MODULE_WINDOWS_RUNTIME) != 0);
    }

    bool IsNGEN() const
    {
        return ((flags & COR_PRF_MODULE_NGEN) != 0);
    }

    bool IsDynamic() const
    {
        return ((flags & COR_PRF_MODULE_DYNAMIC) != 0);
    }

    bool IsResource() const
    {
        return ((flags & COR_PRF_MODULE_RESOURCE) != 0);
    }
};

struct TypeInfo
{
    const mdToken id;
    const shared::WSTRING name;
    const mdTypeSpec type_spec;
    const ULONG32 token_type;
    std::shared_ptr<TypeInfo> extend_from;
    const bool valueType;
    const bool isGeneric;
    std::shared_ptr<TypeInfo> parent_type;
    const mdToken scopeToken;

    TypeInfo() :
        id(0),
        name(shared::EmptyWStr),
        type_spec(0),
        token_type(0),
        extend_from(nullptr),
        valueType(false),
        isGeneric(false),
        parent_type(nullptr),
        scopeToken(0)
    {
    }
    TypeInfo(mdToken id, shared::WSTRING name, mdTypeSpec type_spec, ULONG32 token_type, std::shared_ptr<TypeInfo> extend_from,
             bool valueType, bool isGeneric, std::shared_ptr<TypeInfo> parent_type, mdToken scopeToken) :
        id(id),
        name(name),
        type_spec(type_spec),
        token_type(token_type),
        extend_from(extend_from),
        valueType(valueType),
        isGeneric(isGeneric),
        parent_type(parent_type),
        scopeToken(scopeToken)
    {
    }

    bool IsValid() const
    {
        return id != 0;
    }
};

enum MethodArgumentTypeFlag
{
    TypeFlagByRef = 0x01,
    TypeFlagVoid = 0x02,
    TypeFlagBoxedType = 0x04
};

// Represents a segment inside a larger signature (Method Signature / Local Var Signature) of
// an Argument, Local or Return Value.
struct TypeSignature
{
    ULONG offset;
    ULONG length;
    PCCOR_SIGNATURE pbBase;
    mdToken GetTypeTok(ComPtr<IMetaDataEmit2>& pEmit, mdAssemblyRef corLibRef) const;
    shared::WSTRING GetTypeTokName(ComPtr<IMetaDataImport2>& pImport) const;
    std::tuple<unsigned, int> GetElementTypeAndFlags() const;
    ULONG GetSignature(PCCOR_SIGNATURE& data) const;
};

struct FunctionMethodSignature
{
private:
    PCCOR_SIGNATURE pbBase;
    unsigned len;
    ULONG numberOfTypeArguments = 0;
    ULONG numberOfArguments = 0;
    TypeSignature returnValue{};
    std::vector<TypeSignature> params;

public:
    FunctionMethodSignature() : pbBase(nullptr), len(0)
    {
    }
    FunctionMethodSignature(PCCOR_SIGNATURE pb, unsigned cbBuffer)
    {
        pbBase = pb;
        len = cbBuffer;
    };
    ULONG NumberOfTypeArguments() const
    {
        return numberOfTypeArguments;
    }
    ULONG NumberOfArguments() const
    {
        return numberOfArguments;
    }
    shared::WSTRING str() const
    {
        return shared::HexStr(pbBase, len);
    }
    TypeSignature GetReturnValue() const
    {
        return returnValue;
    }
    const std::vector<TypeSignature>& GetMethodArguments() const
    {
        return params;
    }
    HRESULT TryParse();
    bool operator==(const FunctionMethodSignature& other) const
    {
        return memcmp(pbBase, other.pbBase, len);
    }
    CorCallingConvention CallingConvention() const
    {
        return CorCallingConvention(len == 0 ? 0 : pbBase[0]);
    }
    bool IsEmpty() const
    {
        return len == 0;
    }
};

struct FunctionLocalSignature
{
private:
    PCCOR_SIGNATURE pbBase;
    unsigned len;
    ULONG numberOfLocals = 0;
    std::vector<TypeSignature> locals;

public:
    FunctionLocalSignature() : pbBase(nullptr), len(0)
    {
    }
    FunctionLocalSignature(PCCOR_SIGNATURE pb, unsigned cbBuffer, std::vector<TypeSignature>&& localsSigs) :
        pbBase(pb), len(cbBuffer), numberOfLocals(static_cast<ULONG>(localsSigs.size())), locals(std::move(localsSigs))
    {
    }
    ULONG NumberOfLocals() const
    {
        return numberOfLocals;
    }
    shared::WSTRING str() const
    {
        return shared::HexStr(pbBase, len);
    }
    const std::vector<TypeSignature>& GetMethodLocals() const
    {
        return locals;
    }
    static HRESULT TryParse(PCCOR_SIGNATURE pbBase, unsigned len, std::vector<TypeSignature>& locals);
    bool operator==(const FunctionLocalSignature& other) const
    {
        return memcmp(pbBase, other.pbBase, len);
    }
    bool IsEmpty() const
    {
        return len == 0;
    }
};

struct FunctionInfo
{
    const mdToken id;
    const shared::WSTRING name;
    const TypeInfo type;
    const BOOL is_generic;
    const MethodSignature signature;
    const MethodSignature function_spec_signature;
    const mdToken method_def_id;
    FunctionMethodSignature method_signature;

    FunctionInfo() : id(0), name(shared::EmptyWStr), type({}), is_generic(false), method_def_id(0), method_signature({})
    {
    }

    FunctionInfo(mdToken id, shared::WSTRING name, TypeInfo type, MethodSignature signature,
                 MethodSignature function_spec_signature, mdToken method_def_id,
                 FunctionMethodSignature method_signature) :
        id(id),
        name(name),
        type(type),
        is_generic(true),
        signature(signature),
        function_spec_signature(function_spec_signature),
        method_def_id(method_def_id),
        method_signature(method_signature)
    {
    }

    FunctionInfo(mdToken id, shared::WSTRING name, TypeInfo type, MethodSignature signature,
                 FunctionMethodSignature method_signature) :
        id(id),
        name(name),
        type(type),
        is_generic(false),
        signature(signature),
        method_def_id(0),
        method_signature(method_signature)
    {
    }

    bool IsValid() const
    {
        return id != 0;
    }
};

RuntimeInformation GetRuntimeInformation(ICorProfilerInfo4* info);

AssemblyInfo GetAssemblyInfo(ICorProfilerInfo4* info, const AssemblyID& assembly_id);

AssemblyMetadata GetAssemblyImportMetadata(const ComPtr<IMetaDataAssemblyImport>& assembly_import);

AssemblyMetadata GetReferencedAssemblyMetadata(const ComPtr<IMetaDataAssemblyImport>& assembly_import,
                                               const mdAssemblyRef& assembly_ref);

FunctionInfo GetFunctionInfo(const ComPtr<IMetaDataImport2>& metadata_import, const mdToken& token);

ModuleInfo GetModuleInfo(ICorProfilerInfo4* info, const ModuleID& module_id);

TypeInfo GetTypeInfo(const ComPtr<IMetaDataImport2>& metadata_import, const mdToken& token);

mdAssemblyRef FindAssemblyRef(const ComPtr<IMetaDataAssemblyImport>& assembly_import,
                              const shared::WSTRING& assembly_name, const Version& version);

HRESULT GetCorLibAssemblyRef(const ComPtr<IMetaDataAssemblyEmit>& assembly_emit, AssemblyProperty& corAssemblyProperty,
                             mdAssemblyRef* corlib_ref);

bool FindTypeDefByName(const shared::WSTRING instrumentationTargetMethodTypeName, const shared::WSTRING assemblyName,
                       const ComPtr<IMetaDataImport2>& metadata_import, mdTypeDef& typeDef);
} // namespace trace

#endif // DD_CLR_PROFILER_CLR_HELPERS_H_
