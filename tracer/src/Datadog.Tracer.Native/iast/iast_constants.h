#pragma once
#include "../../../../shared/src/native-src/string.h"
using namespace shared;

namespace iast
{
    namespace Constants
    {
        const BYTE DDAssemblyPublicKeyToken[] = {0xde, 0xf8, 0x6d, 0x06, 0x1d, 0x0d, 0x2e, 0xeb};
        const BYTE MscorlibAssemblyPublicKeyToken[] = {0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89};

        const WSTRING BridgeTypeName = WStr("DDBridge");

        const WSTRING mscorlib = WStr("mscorlib");
        const WSTRING System = WStr("System");
        const WSTRING SystemPrivateCoreLib = WStr("System.Private.CoreLib");
        const WSTRING SystemRuntimeExtensions = WStr("System.Runtime.Extensions");
        const WSTRING SystemCore = WStr("System.Core");
        const WSTRING SystemWeb = WStr("System.Web");
        const WSTRING SystemWebHttpUtility = WStr("System.Web.HttpUtility");
        const WSTRING AspNetCoreModule = WStr("Microsoft.AspNetCore");
        const WSTRING AspNetCoreHostingModule = WStr("Microsoft.AspNetCore.Hosting");
        const WSTRING ExtensionsHostingModule = WStr("Microsoft.Extensions.Hosting");
        const WSTRING MvcViewFeaturesModuleInfoCore = WStr("Microsoft.AspNetCore.Mvc.ViewFeatures");
        const WSTRING MvcCoreModuleInfo = WStr("Microsoft.AspNetCore.Mvc.Core");
        const WSTRING RoutingAbstractionsCoreModuleInfo = WStr("Microsoft.AspNetCore.Routing.Abstractions");
        const WSTRING AspNetCoreHtmlAbstractions = WStr("Microsoft.AspNetCore.Html.Abstractions");
        const WSTRING AspNetCoreServerIisModule = WStr("Microsoft.AspNetCore.Server.IIS");
        const WSTRING AspNetCoreMvcViewFeatures = WStr("Microsoft.AspNetCore.Mvc.ViewFeatures");

        const WSTRING AspectsAssemblyName = WStr("Datadog.Trace");
        const WSTRING AspectsAssemblyFileName = AspectsAssemblyName + WStr(".dll");
        const WSTRING AspectsFileName = AspectsAssemblyName + WStr(".aspects");

        const WSTRING VulnerabilityType_SqlInjection = WStr("SQL_INJECTION");
        const WSTRING VulnerabilityType_XSS = WStr("XSS");
    }
}  // namespace trace
