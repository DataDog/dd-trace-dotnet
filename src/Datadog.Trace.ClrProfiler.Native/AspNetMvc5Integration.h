#pragma once

#include "Integration.h"
#include "TypeReference.h"
#include "MemberReference.h"
#include "GlobalTypeReferences.h"

const TypeReference System_Web_Mvc_ControllerActionInvoker = {
    ELEMENT_TYPE_CLASS,
    L"System.Web.Mvc",
    L"System.Web.Mvc.ControllerActionInvoker"
};

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

Integration AspNetMvc5Integration( /* IsEnabled */
                                      true,
                                      IntegrationType_AspNetMvc5,
                                      std::vector<MemberReference>{
                                          System_Web_Mvc_ControllerActionInvoker_InvokeActionMethod
                                      });
