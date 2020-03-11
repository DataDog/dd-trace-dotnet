#ifndef DD_CLR_PROFILER_INTEGRATION_H_
#define DD_CLR_PROFILER_INTEGRATION_H_

#include <corhlpr.h>
#include <iomanip>
#include <sstream>
#include <vector>

#include "string.h"

#undef major
#undef minor

namespace trace {

const size_t kPublicKeySize = 8;

// PublicKey represents an Assembly Public Key token, which is an 8 byte binary
// RSA key.
struct PublicKey {
  const BYTE data[kPublicKeySize];

  PublicKey() : data{0} {}
  PublicKey(const BYTE (&arr)[kPublicKeySize])
      : data{arr[0], arr[1], arr[2], arr[3], arr[4], arr[5], arr[6], arr[7]} {}

  inline bool operator==(const PublicKey& other) const {
    for (int i = 0; i < kPublicKeySize; i++) {
      if (data[i] != other.data[i]) {
        return false;
      }
    }
    return true;
  }

  inline WSTRING str() const {
    std::stringstream ss;
    for (int i = 0; i < kPublicKeySize; i++) {
      ss << std::setfill('0') << std::setw(2) << std::hex
         << static_cast<int>(data[i]);
    }
    return ToWSTRING(ss.str());
  }
};

// Version is an Assembly version in the form Major.Minor.Build.Revision
// (1.0.0.0)
struct Version {
  const unsigned short major;
  const unsigned short minor;
  const unsigned short build;
  const unsigned short revision;

  Version() : major(0), minor(0), build(0), revision(0) {}
  Version(const unsigned short major, const unsigned short minor,
          const unsigned short build, const unsigned short revision)
      : major(major), minor(minor), build(build), revision(revision) {}

  inline bool operator==(const Version& other) const {
    return major == other.major && minor == other.minor &&
           build == other.build && revision == other.revision;
  }

  inline WSTRING str() const {
    WSTRINGSTREAM ss;
    ss << major << "."_W << minor << "."_W << build << "."_W << revision;
    return ss.str();
  }

  inline bool operator<(const Version& other) const {
    if (major < other.major) {
      return true;
    }
    if (major == other.major && minor < other.minor) {
      return true;
    }
    if (major == other.major && minor == other.minor && build < other.build) {
      return true;
    }
    return false;
  }

  inline bool operator>(const Version& other) const {
    if (major > other.major) {
      return true;
    }
    if (major == other.major && minor > other.minor) {
      return true;
    }
    if (major == other.major && minor == other.minor && build > other.build) {
      return true;
    }
    return false;
  }
};

// An AssemblyReference is a reference to a .Net assembly. In general it will
// look like:
//     Some.Assembly.Name, Version=1.0.0.0, Culture=neutral,
//     PublicKeyToken=abcdef0123456789
struct AssemblyReference {
  const WSTRING name;
  const Version version;
  const WSTRING locale;
  const PublicKey public_key;

  AssemblyReference() {}
  AssemblyReference(const WSTRING& str);

  inline bool operator==(const AssemblyReference& other) const {
    return name == other.name && version == other.version &&
           locale == other.locale && public_key == other.public_key;
  }

  inline WSTRING str() const {
    WSTRINGSTREAM ss;
    ss << name << ", Version="_W << version.str() << ", Culture="_W << locale
       << ", PublicKeyToken="_W << public_key.str();
    return ss.str();
  }
};

// A MethodSignature is a byte array. The format is:
// [calling convention, number of parameters, return type, parameter type...]
// For types see CorElementType
struct MethodSignature {
 public:
  const std::vector<BYTE> data;

  MethodSignature() {}
  MethodSignature(const std::vector<BYTE>& data) : data(data) {}

  inline bool operator==(const MethodSignature& other) const {
    return data == other.data;
  }

  CorCallingConvention CallingConvention() const {
    return CorCallingConvention(data.empty() ? 0 : data[0]);
  }

  size_t NumberOfTypeArguments() const {
    if (data.size() > 1 &&
        (CallingConvention() & IMAGE_CEE_CS_CALLCONV_GENERIC) != 0) {
      return data[1];
    }
    return 0;
  }

  size_t NumberOfArguments() const {
    if (data.size() > 2 &&
        (CallingConvention() & IMAGE_CEE_CS_CALLCONV_GENERIC) != 0) {
      return data[2];
    }
    if (data.size() > 1) {
      return data[1];
    }
    return 0;
  }

  bool ReturnTypeIsObject() const {
    if (data.size() > 2 &&
        (CallingConvention() & IMAGE_CEE_CS_CALLCONV_GENERIC) != 0) {
      return data[3] == ELEMENT_TYPE_OBJECT;
    }
    if (data.size() > 1) {
      return data[2] == ELEMENT_TYPE_OBJECT;
    }

    return false;
  }

  WSTRING str() const {
    WSTRINGSTREAM ss;
    for (auto& b : data) {
      ss << std::hex << std::setfill('0'_W) << std::setw(2) << static_cast<int>(b);
    }
    return ss.str();
  }

  BOOL IsInstanceMethod() const {
    return (CallingConvention() & IMAGE_CEE_CS_CALLCONV_HASTHIS) != 0;
  }
};

struct MethodReference {
  const AssemblyReference assembly;
  const WSTRING type_name;
  const WSTRING method_name;
  const WSTRING action;
  const MethodSignature method_signature;
  const Version min_version;
  const Version max_version;
  const std::vector<WSTRING> signature_types;

  MethodReference()
      : min_version(Version(0, 0, 0, 0)),
        max_version(Version(USHRT_MAX, USHRT_MAX, USHRT_MAX, USHRT_MAX)) {}

  MethodReference(const WSTRING& assembly_name, WSTRING type_name, WSTRING method_name,
                  WSTRING action, Version min_version, Version max_version,
                  const std::vector<BYTE>& method_signature,
                  const std::vector<WSTRING>& signature_types)
      : assembly(assembly_name),
        type_name(type_name),
        method_name(method_name),
        action(action),
        method_signature(method_signature),
        min_version(min_version),
        max_version(max_version),
        signature_types(signature_types) {}

  inline WSTRING get_type_cache_key() const {
    return "["_W + assembly.name + "]"_W + type_name + "_vMin_"_W +
           min_version.str() + "_vMax_"_W + max_version.str();
  }

  inline WSTRING get_method_cache_key() const {
    return "["_W + assembly.name + "]"_W + type_name + "."_W + method_name +
           "_vMin_"_W + min_version.str() + "_vMax_"_W + max_version.str();
  }

  inline bool operator==(const MethodReference& other) const {
    return assembly == other.assembly && type_name == other.type_name &&
           min_version == other.min_version &&
           max_version == other.max_version &&
           method_name == other.method_name &&
           method_signature == other.method_signature;
  }
};

struct MethodReplacement {
  const MethodReference caller_method;
  const MethodReference target_method;
  const MethodReference wrapper_method;

  MethodReplacement() {}

  MethodReplacement(MethodReference caller_method,
                    MethodReference target_method,
                    MethodReference wrapper_method)
      : caller_method(caller_method),
        target_method(target_method),
        wrapper_method(wrapper_method) {}

  inline bool operator==(const MethodReplacement& other) const {
    return caller_method == other.caller_method &&
           target_method == other.target_method &&
           wrapper_method == other.wrapper_method;
  }
};

struct Integration {
  const WSTRING integration_name;
  std::vector<MethodReplacement> method_replacements;

  Integration() : integration_name(""_W), method_replacements({}) {}

  Integration(WSTRING integration_name,
              std::vector<MethodReplacement> method_replacements)
      : integration_name(integration_name),
        method_replacements(method_replacements) {}

  inline bool operator==(const Integration& other) const {
    return integration_name == other.integration_name &&
           method_replacements == other.method_replacements;
  }
};

struct IntegrationMethod {
  const WSTRING integration_name;
  MethodReplacement replacement;

  IntegrationMethod() : integration_name(""_W), replacement({}) {}

  IntegrationMethod(WSTRING integration_name, MethodReplacement replacement)
      : integration_name(integration_name), replacement(replacement) {}

  inline bool operator==(const IntegrationMethod& other) const {
    return integration_name == other.integration_name &&
           replacement == other.replacement;
  }
};

namespace {

WSTRING GetNameFromAssemblyReferenceString(const WSTRING& wstr);
Version GetVersionFromAssemblyReferenceString(const WSTRING& wstr);
WSTRING GetLocaleFromAssemblyReferenceString(const WSTRING& wstr);
PublicKey GetPublicKeyFromAssemblyReferenceString(const WSTRING& wstr);

}  // namespace

}  // namespace trace

#endif  // DD_CLR_PROFILER_INTEGRATION_H_
