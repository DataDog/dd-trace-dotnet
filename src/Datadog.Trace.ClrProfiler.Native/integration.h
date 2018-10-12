#ifndef DD_CLR_PROFILER_INTEGRATION_H_
#define DD_CLR_PROFILER_INTEGRATION_H_

#include <corhlpr.h>
#include <codecvt>
#include <iomanip>
#include <iostream>
#include <locale>
#include <sstream>
#include <string>
#include <vector>
#include "util.h"

#undef minor
#undef major

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

  inline std::u16string str() const {
    std::wostringstream ss;
    for (int i = 0; i < kPublicKeySize; i++) {
      ss << std::setfill(L'0') << std::setw(2) << std::hex << data[i];
    }
    return ToU16(ss.str());
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

  inline std::u16string str() const {
    std::ostringstream ss;
    ss << major << "." << minor << "." << build << "." << revision;
    return ToU16(ss.str());
  }
};

// An AssemblyReference is a reference to a .Net assembly. In general it will
// look like:
//     Some.Assembly.Name, Version=1.0.0.0, Culture=neutral,
//     PublicKeyToken=abcdef0123456789
struct AssemblyReference {
  const std::u16string name;
  const Version version;
  const std::u16string locale;
  const PublicKey public_key;

  AssemblyReference() {}
  AssemblyReference(const std::u16string& str);

  inline bool operator==(const AssemblyReference& other) const {
    return name == other.name && version == other.version &&
           locale == other.locale && public_key == other.public_key;
  }

  inline std::u16string str() const {
    std::ostringstream ss;
    ss << ToU8(name) << ", Version=" << ToU8(version.str())
       << ", Culture=" << ToU8(locale)
       << ", PublicKeyToken=" << ToU8(public_key.str());
    return ToU16(ss.str());
  }
};

// A MethodSignature is a byte array. The format is:
// [calling convention, number of parameters, return type, parameter type...]
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

  std::u16string str() const {
    std::stringstream ss;
    for (auto& b : data) {
      ss << std::hex << std::setfill('0') << std::setw(2) << b;
    }
    return ToU16(ss.str());
  }
};

struct MethodReference {
  const AssemblyReference assembly;
  const std::u16string type_name;
  const std::u16string method_name;
  const MethodSignature method_signature;

  MethodReference() {}

  MethodReference(const std::u16string& assembly_name, std::u16string type_name,
                  std::u16string method_name,
                  const std::vector<BYTE>& method_signature)
      : assembly(assembly_name),
        type_name(std::move(type_name)),
        method_name(std::move(method_name)),
        method_signature(method_signature) {}

  inline std::u16string get_type_cache_key() const {
    return u"[" + assembly.name + u"]" + type_name;
  }

  inline std::u16string get_method_cache_key() const {
    return u"[" + assembly.name + u"]" + type_name + u"." + method_name;
  }

  inline bool operator==(const MethodReference& other) const {
    return assembly == other.assembly && type_name == other.type_name &&
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
      : caller_method(std::move(caller_method)),
        target_method(std::move(target_method)),
        wrapper_method(std::move(wrapper_method)) {}

  inline bool operator==(const MethodReplacement& other) const {
    return caller_method == other.caller_method &&
           target_method == other.target_method &&
           wrapper_method == other.wrapper_method;
  }
};

struct Integration {
  const std::u16string integration_name;
  std::vector<MethodReplacement> method_replacements;

  Integration() : integration_name(u""), method_replacements({}) {}

  Integration(std::u16string integration_name,
              std::vector<MethodReplacement> method_replacements)
      : integration_name(std::move(integration_name)),
        method_replacements(std::move(method_replacements)) {}

  inline bool operator==(const Integration& other) const {
    return integration_name == other.integration_name &&
           method_replacements == other.method_replacements;
  }
};

namespace {

std::u16string GetNameFromAssemblyReferenceString(const std::u16string& wstr);
Version GetVersionFromAssemblyReferenceString(const std::u16string& wstr);
std::u16string GetLocaleFromAssemblyReferenceString(const std::u16string& wstr);
PublicKey GetPublicKeyFromAssemblyReferenceString(const std::u16string& wstr);

}  // namespace

}  // namespace trace

#endif  // DD_CLR_PROFILER_INTEGRATION_H_
