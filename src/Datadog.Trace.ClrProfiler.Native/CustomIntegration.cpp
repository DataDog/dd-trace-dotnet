#include "CustomIntegration.h"
#include "GlobalTypeReferences.h"

CustomIntegration::CustomIntegration()
{
    // TODO: for CustomIntegration, load these from configuration
    m_InstrumentedMethods = {
        ConsoleApp_StaticMethods_DoStuff,
        ConsoleApp_StaticMethods_DoStuffWithArguments,
        ConsoleApp_StaticMethods_DoStuffAndReturnClass,
        ConsoleApp_StaticMethods_DoStuffAndReturnValueType,
        ConsoleApp_InstanceMethods_DoStuff,
        ConsoleApp_InstanceMethods_DoStuffWithArguments,
        ConsoleApp_InstanceMethods_DoStuffAndReturnClass,
        ConsoleApp_InstanceMethods_DoStuffAndReturnValueType,
    };
}

bool CustomIntegration::IsEnabled() const
{
    // TODO: read from configuration
    return true;
}
