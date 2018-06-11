#include "CustomIntegration.h"
#include "GlobalTypeReferences.h"

CustomIntegration::CustomIntegration()
{
    // TODO: load these from configuration
    m_InstrumentedMethods = {
        { L"ConsoleApp.exe", L"ConsoleApp.Program", L"DoStuff", GlobalTypeReferences.System_Void },
        { L"ConsoleApp.exe", L"ConsoleApp.Program", L"DoStuffAndReturnValue", GlobalTypeReferences.System_Int32 },
        { L"ConsoleApp.exe", L"ConsoleApp.Work1", L"DoStuff1", GlobalTypeReferences.System_Void },
        { L"ConsoleApp.exe", L"ConsoleApp.Work2", L"DoStuff2", GlobalTypeReferences.System_Void },
    };
}

bool CustomIntegration::IsEnabled() const
{
    // TODO: read from configuration
    return true;
}
