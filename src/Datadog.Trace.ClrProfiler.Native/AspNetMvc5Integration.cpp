#include "AspNetMvc5Integration.h"
#include "GlobalTypeReferences.h"

// TODO: look into defining integrations in an external configuration file (JSON?) instead of compiled code
AspNetMvc5Integration::AspNetMvc5Integration()
{
    m_InstrumentedMethods = {
        System_Web_Mvc_ControllerActionInvoker_InvokeAction,
    };

    m_TypeReferences = {
        System_Web_Mvc_ControllerContext,
        System_Web_HttpContextBase,
        System_Web_Routing_RouteData,
        System_Web_Routing_RouteBase,
        System_Web_Routing_Route,
        System_Web_Routing_RouteValueDictionary,
    };

    m_MemberReferences = {
        System_Web_Mvc_ControllerContext_get_HttpContext,
        System_Web_Mvc_ControllerContext_get_RouteData,
        System_Web_Routing_RouteData_get_Route,
        System_Web_Routing_Route_get_Url,
        System_Web_Routing_RouteData_get_Values,
    };
}

bool AspNetMvc5Integration::IsEnabled() const
{
    // TODO: read from configuration
    return true;
}
