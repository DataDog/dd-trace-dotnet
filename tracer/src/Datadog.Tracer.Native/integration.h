#ifndef DD_CLR_PROFILER_INTEGRATION_H_
#define DD_CLR_PROFILER_INTEGRATION_H_

#include <corhlpr.h>
#include <iomanip>
#include <sstream>
#include <vector>
#include <unordered_set>
#include "cor.h"
#include "corprof.h"

#include "../../../shared/src/native-src/string.h"

#undef major
#undef minor

namespace trace
{

const size_t kPublicKeySize = 8;

const shared::WSTRING traceattribute_typename = WStr("Datadog.Trace.Annotations.TraceAttribute");
const shared::WSTRING newrelic_traceattribute_typename = WStr("NewRelic.Api.Agent.TraceAttribute");
const shared::WSTRING newrelic_transactionattribute_typename = WStr("NewRelic.Api.Agent.TransactionAttribute");
static LPCWSTR traceAttribute_typename_cstring = traceattribute_typename.c_str();
static LPCWSTR newrelic_traceattribute_typename_cstring = newrelic_traceattribute_typename.c_str();
static LPCWSTR newrelic_transactionattribute_typename_cstring = newrelic_transactionattribute_typename.c_str();

const shared::WSTRING tracemethodintegration_assemblyname = WStr("#TraceMethodFeature");
const std::unordered_set<shared::WSTRING> tracemethodintegration_wildcard_ignored_methods(
    {WStr(".ctor"), WStr(".cctor"), WStr("Equals"), WStr("Finalize"), WStr("GetHashCode"), WStr("ToString")});
const shared::WSTRING tracemethodintegration_wildcardmethodname = WStr("*");
const shared::WSTRING tracemethodintegration_setterprefix = WStr("set_");
const shared::WSTRING tracemethodintegration_getterprefix = WStr("get_");

// PublicKey represents an Assembly Public Key token, which is an 8 byte binary
// RSA key.
struct PublicKey
{
    const BYTE data[kPublicKeySize];

    PublicKey() : data{0}
    {
    }
    PublicKey(const BYTE (&arr)[kPublicKeySize]) : data{arr[0], arr[1], arr[2], arr[3], arr[4], arr[5], arr[6], arr[7]}
    {
    }

    inline bool operator==(const PublicKey& other) const
    {
        for (int i = 0; i < kPublicKeySize; i++)
        {
            if (data[i] != other.data[i])
            {
                return false;
            }
        }
        return true;
    }

    inline shared::WSTRING str() const
    {
        std::stringstream ss;
        for (int i = 0; i < kPublicKeySize; i++)
        {
            ss << std::setfill('0') << std::setw(2) << std::hex << static_cast<int>(data[i]);
        }
        return shared::ToWSTRING(ss.str());
    }
};

// Version is an Assembly version in the form Major.Minor.Build.Revision
// (1.0.0.0)
struct Version
{
    const unsigned short major;
    const unsigned short minor;
    const unsigned short build;
    const unsigned short revision;

    Version() : major(0), minor(0), build(0), revision(0)
    {
    }
    Version(const unsigned short major, const unsigned short minor, const unsigned short build,
            const unsigned short revision) :
        major(major), minor(minor), build(build), revision(revision)
    {
    }

    inline bool operator==(const Version& other) const
    {
        return major == other.major && minor == other.minor && build == other.build && revision == other.revision;
    }

    inline bool operator!=(const Version& other) const
    {
        return !(*this == other);
    }

    inline shared::WSTRING str() const
    {
        std::stringstream ss;
        ss << major << "." << minor << "." << build << "." << revision;
        return shared::ToWSTRING(ss.str());
    }

    inline bool operator<(const Version& other) const
    {
        if (major < other.major)
        {
            return true;
        }
        if (major == other.major && minor < other.minor)
        {
            return true;
        }
        if (major == other.major && minor == other.minor && build < other.build)
        {
            return true;
        }
        return false;
    }

    inline bool operator>(const Version& other) const
    {
        return other < *this;
    }

    inline bool operator<=(const Version& other) const
    {
        return !(*this > other);
    }

    inline bool operator>=(const Version& other) const
    {
        return !(*this < other);
    }
};

// An AssemblyReference is a reference to a .Net assembly. In general it will
// look like:
//     Some.Assembly.Name, Version=1.0.0.0, Culture=neutral,
//     PublicKeyToken=abcdef0123456789
struct AssemblyReference
{
    const shared::WSTRING name;
    const Version version;
    const shared::WSTRING locale;
    const PublicKey public_key;

    AssemblyReference()
    {
    }
    AssemblyReference(const shared::WSTRING& str);

    inline bool operator==(const AssemblyReference& other) const
    {
        return name == other.name && version == other.version && locale == other.locale &&
               public_key == other.public_key;
    }

    inline shared::WSTRING str() const
    {
        const auto ss = name + WStr(", Version=") + version.str() + WStr(", Culture=") + locale +
                        WStr(", PublicKeyToken=") + public_key.str();
        return ss;
    }

    static AssemblyReference* GetFromCache(const shared::WSTRING& str);
};

// A MethodSignature is a byte array. The format is:
// [calling convention, number of parameters, return type, parameter type...]
// For types see CorElementType
struct MethodSignature
{
public:
    const std::vector<BYTE> data;

    MethodSignature()
    {
    }
    MethodSignature(const std::vector<BYTE>& data) : data(data)
    {
    }

    inline bool operator==(const MethodSignature& other) const
    {
        return data == other.data;
    }

    CorCallingConvention CallingConvention() const
    {
        return CorCallingConvention(data.empty() ? 0 : data[0]);
    }

    size_t NumberOfTypeArguments() const
    {
        if (data.size() > 1 && (CallingConvention() & IMAGE_CEE_CS_CALLCONV_GENERIC) != 0)
        {
            return data[1];
        }
        return 0;
    }

    size_t NumberOfArguments() const
    {
        if (data.size() > 2 && (CallingConvention() & IMAGE_CEE_CS_CALLCONV_GENERIC) != 0)
        {
            return data[2];
        }
        if (data.size() > 1)
        {
            return data[1];
        }
        return 0;
    }

    bool ReturnTypeIsObject() const
    {
        if (data.size() > 2 && (CallingConvention() & IMAGE_CEE_CS_CALLCONV_GENERIC) != 0)
        {
            return data[3] == ELEMENT_TYPE_OBJECT;
        }
        if (data.size() > 1)
        {
            return data[2] == ELEMENT_TYPE_OBJECT;
        }

        return false;
    }

    size_t IndexOfReturnType() const
    {
        if (data.size() > 2 && (CallingConvention() & IMAGE_CEE_CS_CALLCONV_GENERIC) != 0)
        {
            return 3;
        }
        if (data.size() > 1)
        {
            return 2;
        }
        return 0;
    }

    shared::WSTRING str() const
    {
        std::stringstream ss;
        for (auto& b : data)
        {
            ss << std::hex << std::setfill('0') << std::setw(2) << static_cast<int>(b);
        }
        return shared::ToWSTRING(ss.str());
    }

    BOOL IsInstanceMethod() const
    {
        return (CallingConvention() & IMAGE_CEE_CS_CALLCONV_HASTHIS) != 0;
    }
};

struct TypeReference
{
    const AssemblyReference assembly;
    const shared::WSTRING name;
    const Version min_version;
    const Version max_version;

    TypeReference() : min_version(Version(0, 0, 0, 0)), max_version(Version(USHRT_MAX, USHRT_MAX, USHRT_MAX, USHRT_MAX))
    {
    }

    TypeReference(const shared::WSTRING& assembly_name, shared::WSTRING type_name, Version min_version, Version max_version) :
        assembly(*AssemblyReference::GetFromCache(assembly_name)),
        name(type_name),
        min_version(min_version),
        max_version(max_version)
    {
    }

    inline shared::WSTRING get_cache_key() const
    {
        return WStr("[") + assembly.name + WStr("]") + name + WStr("_vMin_") + min_version.str() + WStr("_vMax_") +
               max_version.str();
    }

    inline bool operator==(const TypeReference& other) const
    {
        return assembly == other.assembly && name == other.name && min_version == other.min_version &&
               max_version == other.max_version;
    }
};

struct MethodReference
{
    const TypeReference type;
    const shared::WSTRING method_name;
    const std::vector<shared::WSTRING> signature_types;

    MethodReference()
    {
    }

    MethodReference(const shared::WSTRING& assembly_name, shared::WSTRING type_name, shared::WSTRING method_name, Version min_version,
                    Version max_version, const std::vector<shared::WSTRING>& signature_types) :
        type(assembly_name, type_name, min_version, max_version),
        method_name(method_name),
        signature_types(signature_types)
    {
    }

    inline bool operator==(const MethodReference& other) const
    {
        return type == other.type && method_name == other.method_name;
    }
};

struct IntegrationDefinition
{
    const MethodReference target_method;
    const TypeReference integration_type;
    const bool is_derived = false;
    const bool is_exact_signature_match = true;

    IntegrationDefinition()
    {
    }

    IntegrationDefinition(const IntegrationDefinition& other) :
        target_method(other.target_method),
        integration_type(other.integration_type),
        is_derived(other.is_derived),
        is_exact_signature_match(other.is_exact_signature_match)
    {
    }

    IntegrationDefinition(MethodReference target_method, TypeReference integration_type, bool isDerived,
                          bool is_exact_signature_match) :
        target_method(target_method),
        integration_type(integration_type),
        is_derived(isDerived),
        is_exact_signature_match(is_exact_signature_match)
    {
    }

    inline bool operator==(const IntegrationDefinition& other) const
    {
        return target_method == other.target_method && integration_type == other.integration_type &&
               is_derived == other.is_derived && is_exact_signature_match == other.is_exact_signature_match;
    }
    
    IntegrationDefinition& operator=(IntegrationDefinition const&) = default;
};

typedef struct _CallTargetDefinition
{
    WCHAR* targetAssembly;
    WCHAR* targetType;
    WCHAR* targetMethod;
    WCHAR** signatureTypes;
    USHORT signatureTypesLength;
    USHORT targetMinimumMajor;
    USHORT targetMinimumMinor;
    USHORT targetMinimumPatch;
    USHORT targetMaximumMajor;
    USHORT targetMaximumMinor;
    USHORT targetMaximumPatch;
    WCHAR* integrationAssembly;
    WCHAR* integrationType;
} CallTargetDefinition;

struct MethodIdentifier
{
    ModuleID moduleId;
    mdMethodDef methodToken;

    MethodIdentifier() : moduleId(0), methodToken(0)
    {
    }

    MethodIdentifier(ModuleID moduleId, mdMethodDef methodToken) : moduleId(moduleId), methodToken(methodToken)
    {
    }

    MethodIdentifier(const MethodIdentifier& copyFrom) : moduleId(copyFrom.moduleId), methodToken(copyFrom.methodToken)
    {
    }

    // Need it to use std::map
    bool operator<(const MethodIdentifier& rhs) const
    {
        return moduleId < rhs.moduleId || (moduleId == rhs.moduleId && methodToken < rhs.methodToken);
    }

    bool operator==(const MethodIdentifier& other) const
    {
        return (moduleId == other.moduleId && methodToken == other.methodToken);
    }
};

namespace
{

    shared::WSTRING GetNameFromAssemblyReferenceString(const shared::WSTRING& wstr);
    Version GetVersionFromAssemblyReferenceString(const shared::WSTRING& wstr);
    shared::WSTRING GetLocaleFromAssemblyReferenceString(const shared::WSTRING& wstr);
    PublicKey GetPublicKeyFromAssemblyReferenceString(const shared::WSTRING& wstr);

} // namespace

    std::vector<IntegrationDefinition> GetIntegrationsFromTraceMethodsConfiguration(const TypeReference integration_type,
                                                                                    const shared::WSTRING& configuration_string);

} // namespace trace

namespace std
{
template <>
struct hash<trace::MethodIdentifier>
{
    size_t operator()(const trace::MethodIdentifier& k) const noexcept
    {
        // Compute individual hash values for first,
        // second and combine them using XOR
        // and bit shifting:

        return ((k.moduleId) ^ static_cast<long long>(k.methodToken) << 1) >> 1;
    }
};
} // namespace std

#endif // DD_CLR_PROFILER_INTEGRATION_H_
