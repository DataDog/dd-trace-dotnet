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
    const TypeReference System_Web_Mvc_ControllerActionInvoker = { ELEMENT_TYPE_OBJECT, L"System.Web.Mvc", L"System.Web.Mvc.ControllerActionInvoker" };
    const TypeReference System_Web_Mvc_ControllerContext = { ELEMENT_TYPE_CLASS, L"System.Web.Mvc", L"System.Web.Mvc.ControllerContext" };

    // callvirt instance bool [System.Web.Mvc]System.Web.Mvc.ControllerActionInvoker::InvokeAction(class [System.Web.Mvc]System.Web.Mvc.ControllerContext, string)
    const MemberReference System_Web_Mvc_ControllerActionInvoker_InvokeAction
    {
        System_Web_Mvc_ControllerActionInvoker,
        L"InvokeActionMethod",
        true,
        IMAGE_CEE_CS_CALLCONV_HASTHIS,
        GlobalTypeReferences.System_Boolean,
        {
            System_Web_Mvc_ControllerContext,
            GlobalTypeReferences.System_String,
        },
    };
};
