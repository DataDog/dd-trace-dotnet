#include "CustomIntegration.h"
#include "GlobalTypeReferences.h"

CustomIntegration::CustomIntegration()
{
    // TODO: for CustomIntegration, load these from configuration
    m_InstrumentedMethods = {
        ConsoleApp_Program_DoStuff,
    };

    m_TypeReferences = {
        // ConsoleApp_Program,
    };

    m_MemberReferences = {
        // ConsoleApp_Program_DoStuff,
    };
}

bool CustomIntegration::IsEnabled() const
{
    // TODO: read from configuration
    return true;
}
