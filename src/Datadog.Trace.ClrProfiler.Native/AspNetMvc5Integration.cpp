#include "AspNetMvc5Integration.h"
#include "GlobalTypeReferences.h"

// TODO: look into defining integrations in an external configuration file (JSON?) instead of compiled code
AspNetMvc5Integration::AspNetMvc5Integration()
{
    m_InstrumentedMethods = {
        System_Web_Mvc_ControllerActionInvoker_InvokeAction,
    };
}

bool AspNetMvc5Integration::IsEnabled() const
{
    // TODO: read from configuration
    return true;
}
