#pragma once

#include <corhlpr.h>
#include <iomanip>
#include <iostream>
#include <sstream>
#include <vector>
#include "IntegrationType.h"

namespace trace {

const size_t kPublicKeySize = 8;

// PublicKey represents an Assembly Public Key token, which is an 8 byte binary
// RSA key.
struct PublicKey {
  const uint8_t data[kPublicKeySize];

  PublicKey() : data{0} {}
  PublicKey(const uint8_t (&arr)[kPublicKeySize])
      : data{arr[0], arr[1], arr[2], arr[3], arr[4], arr[5], arr[6], arr[7]} {}

  bool operator==(const PublicKey& other) const {
    for (int i = 0; i < kPublicKeySize; i++) {
      if (data[i] != other.data[i]) {
        return false;
      }
    }
    return true;
  }

  std::wstring str() const {
    std::wostringstream ss;
    for (int i = 0; i < kPublicKeySize; i++) {
      ss << std::setfill(L'0') << std::setw(2) << std::hex << data[i];
    }
    return ss.str();
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

  bool operator==(const Version& other) const {
    return major == other.major && minor == other.minor &&
           build == other.build && revision == other.revision;
  }

  std::wstring str() const {
    std::wostringstream ss;
    ss << major << L"." << minor << L"." << build << L"." << revision;
    return ss.str();
  }
};

// An AssemblyReference is a reference to a .Net assembly. In general it will
// look like:
//     Some.Assembly.Name, Version=1.0.0.0, Culture=neutral,
//     PublicKeyToken=abcdef0123456789
struct AssemblyReference {
  const std::wstring name;
  const Version version;
  const std::wstring locale;
  const PublicKey public_key;

  AssemblyReference() {}
  AssemblyReference(const std::wstring& str);

  bool operator==(const AssemblyReference& other) const {
    return name == other.name && version == other.version &&
           locale == other.locale && public_key == other.public_key;
  }

  std::wstring str() const {
    std::wostringstream ss;
    ss << name << L", Version=" << version.str() << L", Culture=" << locale
       << L", PublicKeyToken=" << public_key.str();
    return ss.str();
  }
};

// A MethodSignature is a byte array. The format is:
// [calling convention, number of parameters, return type, parameter type...]
struct MethodSignature {
 public:
  const std::vector<uint8_t> data;

  MethodSignature() {}
  MethodSignature(const std::vector<uint8_t>& data) : data(data) {}
};

namespace {

std::wstring GetNameFromAssemblyReferenceString(const std::wstring& wstr);
Version GetVersionFromAssemblyReferenceString(const std::wstring& wstr);
std::wstring GetLocaleFromAssemblyReferenceString(const std::wstring& wstr);
PublicKey GetPublicKeyFromAssemblyReferenceString(const std::wstring& wstr);

}  // namespace

}  // namespace trace

struct method_reference {
  const trace::AssemblyReference assembly;
  const std::wstring type_name;
  const std::wstring method_name;
  const trace::MethodSignature method_signature;

  method_reference() {}

  method_reference(const std::wstring& assembly_name, std::wstring type_name,
                   std::wstring method_name,
                   const std::vector<uint8_t>& method_signature)
      : assembly(assembly_name),
        type_name(std::move(type_name)),
        method_name(std::move(method_name)),
        method_signature(method_signature) {}

  std::wstring get_type_cache_key() const {
    return L"[" + assembly.name + L"]" + type_name;
  }

  std::wstring get_method_cache_key() const {
    return L"[" + assembly.name + L"]" + type_name + L"." + method_name;
  }
};

struct method_replacement {
  const method_reference caller_method;
  const method_reference target_method;
  const method_reference wrapper_method;

  method_replacement() {}

  method_replacement(method_reference caller_method,
                     method_reference target_method,
                     method_reference wrapper_method)
      : caller_method(std::move(caller_method)),
        target_method(std::move(target_method)),
        wrapper_method(std::move(wrapper_method)) {}
};

struct integration {
  const IntegrationType integration_type;
  const std::wstring integration_name;
  std::vector<method_replacement> method_replacements;

  integration() : integration_type(IntegrationType_Custom) {}

  integration(const IntegrationType integration_type,
              std::wstring integration_name,
              std::vector<method_replacement> method_replacements)
      : integration_type(integration_type),
        integration_name(std::move(integration_name)),
        method_replacements(std::move(method_replacements)) {}
};
