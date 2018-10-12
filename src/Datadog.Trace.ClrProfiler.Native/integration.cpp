#include "integration.h"

#include <regex>
#include <sstream>
#include <string>

namespace trace {

AssemblyReference::AssemblyReference(const std::u16string& ustr)
    : name(GetNameFromAssemblyReferenceString(ustr)),
      version(GetVersionFromAssemblyReferenceString(ustr)),
      locale(GetLocaleFromAssemblyReferenceString(ustr)),
      public_key(GetPublicKeyFromAssemblyReferenceString(ustr)) {}

namespace {

std::u16string GetNameFromAssemblyReferenceString(const std::u16string& ustr) {
  std::u16string name = ustr;

  auto pos = name.find(u',');
  if (pos != std::wstring::npos) {
    name = name.substr(0, pos);
  }

  // strip spaces
  pos = name.rfind(u' ');
  if (pos != std::wstring::npos) {
    name = name.substr(0, pos);
  }

  return name;
}

Version GetVersionFromAssemblyReferenceString(const std::u16string& ustr) {
  auto wstr = ToW(ustr);

  unsigned short major = 0;
  unsigned short minor = 0;
  unsigned short build = 0;
  unsigned short revision = 0;

  static auto re =
      std::wregex(L"Version=([0-9]+)\\.([0-9]+)\\.([0-9]+)\\.([0-9]+)");

  std::wsmatch match;
  if (std::regex_search(wstr, match, re) && match.size() == 5) {
    std::wstringstream(match.str(1)) >> major;
    std::wstringstream(match.str(2)) >> minor;
    std::wstringstream(match.str(3)) >> build;
    std::wstringstream(match.str(4)) >> revision;
  }

  return {major, minor, build, revision};
}

std::u16string GetLocaleFromAssemblyReferenceString(
    const std::u16string& ustr) {
  auto wstr = ToW(ustr);
  std::wstring locale = L"neutral";

  static auto re = std::wregex(L"Culture=([a-zA-Z0-9]+)");
  std::wsmatch match;
  if (std::regex_search(wstr, match, re) && match.size() == 2) {
    locale = match.str(1);
  }

  return ToU16(locale);
}

PublicKey GetPublicKeyFromAssemblyReferenceString(const std::u16string& ustr) {
  auto wstr = ToW(ustr);
  BYTE data[8] = {0};

  static auto re = std::wregex(L"PublicKeyToken=([a-fA-F0-9]{16})");
  std::wsmatch match;
  if (std::regex_search(wstr, match, re) && match.size() == 2) {
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
