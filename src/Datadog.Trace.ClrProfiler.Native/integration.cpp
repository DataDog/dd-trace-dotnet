
#include "integration.h"

#include <re2/re2.h>
#include <sstream>

#include "util.h"

namespace trace {

AssemblyReference::AssemblyReference(const std::wstring& str)
    : name(GetNameFromAssemblyReferenceString(str)),
      version(GetVersionFromAssemblyReferenceString(str)),
      locale(GetLocaleFromAssemblyReferenceString(str)),
      public_key(GetPublicKeyFromAssemblyReferenceString(str)) {}

namespace {

std::wstring GetNameFromAssemblyReferenceString(const std::wstring& wstr) {
  std::wstring name = wstr;

  auto pos = name.find(L',');
  if (pos != std::wstring::npos) {
    name = name.substr(0, pos);
  }

  // strip spaces
  pos = name.rfind(L' ');
  if (pos != std::wstring::npos) {
    name = name.substr(0, pos);
  }

  return name;
}

Version GetVersionFromAssemblyReferenceString(const std::wstring& str) {
  unsigned short major = 0;
  unsigned short minor = 0;
  unsigned short build = 0;
  unsigned short revision = 0;

  static re2::RE2 re("Version=([0-9]+)\\.([0-9]+)\\.([0-9]+)\\.([0-9]+)",
                     RE2::Quiet);
  re2::RE2::FullMatch(ToString(str), re, &major, &minor, &build, &revision);
  return {major, minor, build, revision};
}

std::wstring GetLocaleFromAssemblyReferenceString(const std::wstring& str) {
  std::wstring locale = L"neutral";

  static re2::RE2 re("Culture=([a-zA-Z0-9]+)", RE2::Quiet);

  std::string match;
  if (re2::RE2::FullMatch(ToString(str), re, &match)) {
    locale = ToWString(match);
  }

  return locale;
}

PublicKey GetPublicKeyFromAssemblyReferenceString(const std::wstring& str) {
  BYTE data[8] = {0};

  static re2::RE2 re("PublicKeyToken=([a-fA-F0-9]{16})");
  std::string match;
  if (re2::RE2::FullMatch(ToString(str), re, &match)) {
    for (int i = 0; i < 8; i++) {
      auto s = match.substr(i * 2, 2);
      unsigned long x;
      std::stringstream(s) >> std::hex >> x;
      data[i] = BYTE(x);
    }
  }

  return PublicKey(data);
}

}  // namespace

}  // namespace trace
