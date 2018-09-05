#include <regex>
#include <sstream>

#include "integration.h"

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

  static auto re =
      std::wregex(L"Version=([0-9]+)\\.([0-9]+)\\.([0-9]+)\\.([0-9]+)");

  std::wsmatch match;
  if (std::regex_search(str, match, re) && match.size() == 5) {
    std::wstringstream(match.str(1)) >> major;
    std::wstringstream(match.str(2)) >> minor;
    std::wstringstream(match.str(3)) >> build;
    std::wstringstream(match.str(4)) >> revision;
  }

  return {major, minor, build, revision};
}

std::wstring GetLocaleFromAssemblyReferenceString(const std::wstring& str) {
  std::wstring locale = L"neutral";

  static auto re = std::wregex(L"Culture=([a-zA-Z0-9]+)");
  std::wsmatch match;
  if (std::regex_search(str, match, re) && match.size() == 2) {
    locale = match.str(1);
  }

  return locale;
}

PublicKey GetPublicKeyFromAssemblyReferenceString(const std::wstring& str) {
  BYTE data[8] = {0};

  static auto re = std::wregex(L"PublicKeyToken=([a-fA-F0-9]{16})");
  std::wsmatch match;
  if (std::regex_search(str, match, re) && match.size() == 2) {
    for (int i = 0; i < 8; i++) {
      auto s = match.str(1).substr(i * 2, 2);
      unsigned long x;
      std::wstringstream(s) >> std::hex >> x;
      data[i] = BYTE(x);
    }
  }

  return PublicKey(data);
}

}  // namespace

}  // namespace trace
