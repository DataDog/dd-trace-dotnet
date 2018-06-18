#pragma once

#include "IntegrationBase.h"
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

private:
    // types used by the instrumented method defined below
    const TypeReference System_Web_Mvc_ControllerActionInvoker = { ELEMENT_TYPE_CLASS, L"System.Web.Mvc", L"System.Web.Mvc.ControllerActionInvoker" };

    const MemberReference System_Web_Mvc_ControllerActionInvoker_InvokeActionMethod
    {
        System_Web_Mvc_ControllerActionInvoker,
        L"InvokeActionMethod",
        // IsVirtual
        true,
        IMAGE_CEE_CS_CALLCONV_HASTHIS,
        GlobalTypeReferences.System_Object,
        {
            GlobalTypeReferences.System_Object,
            GlobalTypeReferences.System_Object,
            GlobalTypeReferences.System_Object,
        },
    };
};
