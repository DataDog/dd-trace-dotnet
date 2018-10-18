
#include "integration.h"

#ifdef _WIN32
#include <regex>
#else
#include <re2/re2.h>
#endif
#include <sstream>

#include "util.h"

namespace trace {

AssemblyReference::AssemblyReference(const WSTRING& str)
    : name(GetNameFromAssemblyReferenceString(str)),
      version(GetVersionFromAssemblyReferenceString(str)),
      locale(GetLocaleFromAssemblyReferenceString(str)),
      public_key(GetPublicKeyFromAssemblyReferenceString(str)) {}

namespace {

WSTRING GetNameFromAssemblyReferenceString(const WSTRING& wstr) {
  WSTRING name = wstr;

  auto pos = name.find(','_W);
  if (pos != WSTRING::npos) {
    name = name.substr(0, pos);
  }

  // strip spaces
  pos = name.rfind(' '_W);
  if (pos != WSTRING::npos) {
    name = name.substr(0, pos);
  }

  return name;
}

Version GetVersionFromAssemblyReferenceString(const WSTRING& str) {
  unsigned short major = 0;
  unsigned short minor = 0;
  unsigned short build = 0;
  unsigned short revision = 0;

#ifdef _WIN32

  static auto re =
      std::wregex("Version=([0-9]+)\\.([0-9]+)\\.([0-9]+)\\.([0-9]+)"_W);

  std::wsmatch match;
  if (std::regex_search(str, match, re) && match.size() == 5) {
    WSTRINGSTREAM(match.str(1)) >> major;
    WSTRINGSTREAM(match.str(2)) >> minor;
    WSTRINGSTREAM(match.str(3)) >> build;
    WSTRINGSTREAM(match.str(4)) >> revision;
  }

#else

  static re2::RE2 re("Version=([0-9]+)\\.([0-9]+)\\.([0-9]+)\\.([0-9]+)",
                     RE2::Quiet);
  re2::RE2::FullMatch(ToString(str), re, &major, &minor, &build, &revision);

#endif

  return {major, minor, build, revision};
}

WSTRING GetLocaleFromAssemblyReferenceString(const WSTRING& str) {
  WSTRING locale = "neutral"_W;

#ifdef _WIN32

  static auto re = std::wregex("Culture=([a-zA-Z0-9]+)"_W);
  std::wsmatch match;
  if (std::regex_search(str, match, re) && match.size() == 2) {
    locale = match.str(1);
  }

#else

  static re2::RE2 re("Culture=([a-zA-Z0-9]+)", RE2::Quiet);

  std::string match;
  if (re2::RE2::FullMatch(ToString(str), re, &match)) {
    locale = ToWSTRING(match);
  }

#endif

  return locale;
}

PublicKey GetPublicKeyFromAssemblyReferenceString(const WSTRING& str) {
  BYTE data[8] = {0};

#ifdef _WIN32

  static auto re = std::wregex("PublicKeyToken=([a-fA-F0-9]{16})"_W);
  std::wsmatch match;
  if (std::regex_search(str, match, re) && match.size() == 2) {
    for (int i = 0; i < 8; i++) {
      auto s = match.str(1).substr(i * 2, 2);
      unsigned long x;
      WSTRINGSTREAM(s) >> std::hex >> x;
      data[i] = BYTE(x);
    }
  }

#else

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

#endif

  return PublicKey(data);
}

}  // namespace

}  // namespace trace
