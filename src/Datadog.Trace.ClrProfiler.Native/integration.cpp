#include "integration.h"
#include <regex>
#include <sstream>

namespace trace {

::std::wstring AssemblyReference::GetNameFromString(
    const ::std::wstring& wstr) {
  ::std::wstring name;

  size_t pos;
  if ((pos = wstr.find(L',')) != ::std::wstring::npos) {
    name = wstr.substr(0, pos);
  } else {
    name = wstr;
  }

  // strip spaces
  if ((pos = wstr.rfind(L' ')) != ::std::wstring::npos) {
    name = name.substr(0, pos);
  }

  return name;
}

Version AssemblyReference::GetVersionFromString(const ::std::wstring& str) {
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

std::wstring AssemblyReference::GetLocaleFromString(const std::wstring& str) {
  std::wstring locale = L"neutral";

  static auto re = std::wregex(L"Culture=([a-zA-Z0-9]+)");
  std::wsmatch match;
  if (std::regex_search(str, match, re) && match.size() == 2) {
    locale = match.str(1);
  }

  return locale;
}

PublicKey AssemblyReference::GetPublicKeyFromString(const std::wstring& str) {
  uint8_t data[8] = {0};

  static auto re = std::wregex(L"PublicKeyToken=([a-zA-Z0-9]+)");
  std::wsmatch match;
  if (std::regex_search(str, match, re) && match.size() == 2 &&
      match.str(1).size() == 8 * 2) {
    for (int i = 0; i < 8; i++) {
      auto s = match.str(1).substr(i * 2, 2);
      unsigned long x;
      std::wstringstream(s) >> std::hex >> x;
      data[i] = uint8_t(x);
    }
  }

  return PublicKey(data);
}

}  // namespace trace

const integration aspnet_mvc5_integration = ::integration(
    // integration_type
    IntegrationType_AspNet_Mvc5,
    // integration_name
    L"aspNetMvc5",
    {method_replacement(
         // caller
         method_reference(
             // assembly
             L"System.Web.Mvc",
             // type
             L"",
             // method name
             L"",
             // method signature
             {}),
         // target
         method_reference(
             // assembly
             L"System.Web.Mvc",
             // type
             L"System.Web.Mvc.Async.IAsyncActionInvoker",
             // method name
             L"BeginInvokeAction",
             // method signature
             {}),
         // wrapper
         method_reference(
             // assembly
             L"Datadog.Trace.ClrProfiler.Managed",
             // type
             L"Datadog.Trace.ClrProfiler.Integrations.AspNetMvc5Integration",
             // method name
             L"BeginInvokeAction",
             // method signature
             {
                 // calling convention
                 IMAGE_CEE_CS_CALLCONV_DEFAULT,
                 // parameter count
                 0x05,
                 // return type
                 ELEMENT_TYPE_OBJECT,
                 // parameter types
                 ELEMENT_TYPE_OBJECT,
                 ELEMENT_TYPE_OBJECT,
                 ELEMENT_TYPE_OBJECT,
                 ELEMENT_TYPE_OBJECT,
                 ELEMENT_TYPE_OBJECT,
             })),
     method_replacement(
         // caller method
         method_reference(
             // assembly
             L"System.Web.Mvc",
             // type
             L"",
             // method name
             L"",
             // method signature
             {}),
         // target method
         method_reference(
             // assembly
             L"System.Web.Mvc",
             // type
             L"System.Web.Mvc.Async.IAsyncActionInvoker",
             // method name
             L"EndInvokeAction",
             // method signature
             {}),
         // wrapper method
         method_reference(
             // assembly
             L"Datadog.Trace.ClrProfiler.Managed",
             // type
             L"Datadog.Trace.ClrProfiler.Integrations.AspNetMvc5Integration",
             // method name
             L"EndInvokeAction",
             // method signature
             {
                 // calling convention
                 IMAGE_CEE_CS_CALLCONV_DEFAULT,
                 // parameter count
                 0x02,
                 // return type
                 ELEMENT_TYPE_BOOLEAN,
                 // parameter types
                 ELEMENT_TYPE_OBJECT,
                 ELEMENT_TYPE_OBJECT,
             }))});

const integration aspnetcore_mvc2_integration = ::integration(
    // integration_type
    IntegrationType_AspNetCore_Mvc2,
    // integration_name
    L"aspNetCoreMvc2",
    {method_replacement(
         // caller
         method_reference(
             // assembly
             L"Microsoft.AspNetCore.Mvc.Core",
             // type
             L"Microsoft.AspNetCore.Mvc.Internal.ResourceInvoker",
             // method name
             L"",
             // method signature
             {}),
         // target
         method_reference(
             // assembly
             L"Microsoft.AspNetCore.Mvc.Core",
             // type
             L"Microsoft.AspNetCore.Mvc.Internal."
             L"MvcCoreDiagnosticSourceExtensions",
             // method name
             L"BeforeAction",
             // method signature
             {}),
         // wrapper
         method_reference(
             // assembly
             L"Datadog.Trace.ClrProfiler.Managed",
             // type
             L"Datadog.Trace.ClrProfiler.Integrations."
             L"AspNetCoreMvc2Integration",
             // method name
             L"BeforeAction",
             // method signature
             {
                 // calling convention
                 IMAGE_CEE_CS_CALLCONV_DEFAULT,
                 // parameter count
                 0x04,
                 // return type
                 ELEMENT_TYPE_OBJECT,
                 // parameter types
                 ELEMENT_TYPE_OBJECT,
                 ELEMENT_TYPE_OBJECT,
                 ELEMENT_TYPE_OBJECT,
                 ELEMENT_TYPE_OBJECT,
             })),
     method_replacement(
         // caller method
         method_reference(
             // assembly
             L"Microsoft.AspNetCore.Mvc.Core",
             // type
             L"Microsoft.AspNetCore.Mvc.Internal.ResourceInvoker",
             // method name
             L"",
             // method signature
             {}),
         // target method
         method_reference(
             // assembly
             L"Microsoft.AspNetCore.Mvc.Core",
             // type
             L"Microsoft.AspNetCore.Mvc.Internal."
             L"MvcCoreDiagnosticSourceExtensions",
             // method name
             L"AfterAction",
             // method signature
             {}),
         // wrapper method
         method_reference(
             // assembly
             L"Datadog.Trace.ClrProfiler.Managed",
             // type
             L"Datadog.Trace.ClrProfiler.Integrations."
             L"AspNetCoreMvc2Integration",
             // method name
             L"AfterAction",
             // method signature
             {
                 // calling convention
                 IMAGE_CEE_CS_CALLCONV_DEFAULT,
                 // parameter count
                 0x04,
                 // return type
                 ELEMENT_TYPE_VOID,
                 // parameter types
                 ELEMENT_TYPE_OBJECT,
                 ELEMENT_TYPE_OBJECT,
                 ELEMENT_TYPE_OBJECT,
                 ELEMENT_TYPE_OBJECT,
             }))});

std::vector<integration> default_integrations = {
    aspnet_mvc5_integration,
    aspnetcore_mvc2_integration,
};
