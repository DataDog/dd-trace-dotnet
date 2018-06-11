#include "AspNetMvc5Integration.h"
#include "GlobalTypeReferences.h"

AspNetMvc5Integration::AspNetMvc5Integration()
{
    m_InstrumentedMethods = {
        // async
        // System.Web.Mvc, Version=5.2.6.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35
        // System.Web.Mvc.Async.AsyncControllerActionInvoker.BeginInvokeAsynchronousActionMethod()
        // ControllerContext controllerContext, AsyncActionDescriptor actionDescriptor, IDictionary<string, object> parameters, AsyncCallback callback, object state

        // non-async
        // System.Web.Mvc, Version=5.2.6.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35
        // System.Web.Mvc.ControllerActionInvoker.InvokeActionMethod()
        // ControllerContext controllerContext, ActionDescriptor actionDescriptor, IDictionary<string, object> parameters
        { L"System.Web.Mvc.dll", L"System.Web.Mvc.ControllerActionInvoker", L"InvokeActionMethod", GlobalTypeReferences.System_Object },
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

void AspNetMvc5Integration::InjectEntryArguments(const ILRewriterWrapper& pilr) const
{
    pilr.CreateArray(GlobalTypeReferences.System_Object, 3);

    // 0: HttpContextBase controllerContext.HttpContext
    pilr.BeginLoadValueIntoArray(0);
    pilr.LoadArgument(1); //controllerContext
    pilr.CallMember(System_Web_Mvc_ControllerContext_get_HttpContext);
    pilr.EndLoadValueIntoArray();

    // 1: string ((Route)controllerContext.RouteData.Route).Url (mvc route template)
    pilr.BeginLoadValueIntoArray(1);
    pilr.LoadArgument(1); //controllerContext
    pilr.CallMember(System_Web_Mvc_ControllerContext_get_RouteData);
    pilr.CallMember(System_Web_Routing_RouteData_get_Route);
    pilr.Cast(System_Web_Routing_Route);
    pilr.CallMember(System_Web_Routing_Route_get_Url);
    pilr.EndLoadValueIntoArray();

    // 2: IDictionary<string, object> controllerContext.RouteData.Values (mvc route values)
    pilr.BeginLoadValueIntoArray(2);
    pilr.LoadArgument(1); //controllerContext
    pilr.CallMember(System_Web_Mvc_ControllerContext_get_RouteData);
    pilr.CallMember(System_Web_Routing_RouteData_get_Values);
    pilr.EndLoadValueIntoArray();
}
