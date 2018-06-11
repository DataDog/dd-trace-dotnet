#pragma once

#include "IntegrationBase.h"
#include "ILRewriter.h"
#include "TypeReference.h"
#include "MemberReference.h"
#include "GlobalTypeReferences.h"

class AspNetMvc5Integration : public IntegrationBase
{
public:
    AspNetMvc5Integration();

    bool IsEnabled() const override;

    IntegrationType GetIntegrationType() const override
    {
        return IntegrationType_AspNetMvc5;
    }

protected:
    void InjectEntryArguments(const ILRewriterWrapper& pilr) const override;

private:
    // std::wstring AssemblyName = L"";
    // std::wstring TypeName = L"";
    // CorElementType CorElementType;

    const TypeReference System_Web_Mvc_ControllerContext = { ELEMENT_TYPE_CLASS, L"System.Web.Mvc", L"System.Web.Mvc.ControllerContext" };
    const TypeReference System_Web_HttpContextBase = { ELEMENT_TYPE_CLASS, L"System.Web", L"System.Web.HttpContextBase" };
    const TypeReference System_Web_Routing_RouteData = { ELEMENT_TYPE_CLASS, L"System.Web", L"System.Web.Routing.RouteData" };
    const TypeReference System_Web_Routing_RouteBase = { ELEMENT_TYPE_CLASS, L"System.Web", L"System.Web.Routing.RouteBase" };
    const TypeReference System_Web_Routing_Route = { ELEMENT_TYPE_CLASS, L"System.Web", L"System.Web.Routing.Route" };
    const TypeReference System_Web_Routing_RouteValueDictionary = { ELEMENT_TYPE_CLASS, L"System.Web", L"System.Web.Routing.RouteValueDictionary" };

    // TypeReference Type{};
    // std::wstring MethodName = L"";
    // bool IsVirtual;
    // CorCallingConvention CorCallingConvention;
    // TypeReference ReturnType;
    // std::vector<TypeReference> ArgumentTypes;

    const MemberReference System_Web_Mvc_ControllerContext_get_HttpContext = {
        System_Web_Mvc_ControllerContext,
        L"get_HttpContext",
        // virtual
        true,
        IMAGE_CEE_CS_CALLCONV_HASTHIS,
        System_Web_HttpContextBase
    };

    const MemberReference System_Web_Mvc_ControllerContext_get_RouteData = {
        System_Web_Mvc_ControllerContext,
        L"get_RouteData",
        // IsVirtual
        true,
        IMAGE_CEE_CS_CALLCONV_HASTHIS,
        System_Web_Routing_RouteData
    };

    const MemberReference System_Web_Routing_RouteData_get_Route = {
        System_Web_Routing_RouteData,
        L"get_Route",
        // IsVirtual
        true,
        IMAGE_CEE_CS_CALLCONV_HASTHIS,
        System_Web_Routing_RouteBase
    };

    const MemberReference System_Web_Routing_Route_get_Url = {
        System_Web_Routing_Route,
        L"get_Url",
        // IsVirtual
        true,
        IMAGE_CEE_CS_CALLCONV_HASTHIS,
        GlobalTypeReferences.System_String
    };

    const MemberReference System_Web_Routing_RouteData_get_Values = {
        System_Web_Routing_RouteData,
        L"get_Values",
        // IsVirtual
        true,
        IMAGE_CEE_CS_CALLCONV_HASTHIS,
        System_Web_Routing_RouteValueDictionary
    };
};
