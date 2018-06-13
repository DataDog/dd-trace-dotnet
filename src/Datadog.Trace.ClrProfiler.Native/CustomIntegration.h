#pragma once

#include "IntegrationBase.h"
#include "GlobalTypeReferences.h"

class CustomIntegration : public IntegrationBase
{
public:
    CustomIntegration();

    bool IsEnabled() const override;

    IntegrationType GetIntegrationType() const override
    {
        return IntegrationType_Custom;
    }

private:
    const TypeReference ConsoleApp_Program = { ELEMENT_TYPE_OBJECT, L"ConsoleApp", L"ConsoleApp.Program" };

    // call void ConsoleApp.Program::DoStuff()
    const MemberReference ConsoleApp_Program_DoStuff =
    {
        ConsoleApp_Program,
        L"DoStuff",
        false,
        IMAGE_CEE_CS_CALLCONV_DEFAULT,
        GlobalTypeReferences.System_Void,
        {},
    };

    // call void ConsoleApp.Program::DoStuffWithArguments(string, int32)
    const MemberReference ConsoleApp_Program_DoStuffWithArguments =
    {
        ConsoleApp_Program,
        L"DoStuffWithArguments",
        false,
        IMAGE_CEE_CS_CALLCONV_DEFAULT,
        GlobalTypeReferences.System_Void,
        {
            GlobalTypeReferences.System_String,
            GlobalTypeReferences.System_Int32
        },
    };

    // TODO
    // { L"ConsoleApp.exe", L"ConsoleApp.Program", L"DoStuffAndReturnValue", GlobalTypeReferences.System_Int32 },
    // { L"ConsoleApp.exe", L"ConsoleApp.Program", L"DoStuffWithTryBlock", GlobalTypeReferences.System_Void },
    // { L"ConsoleApp.exe", L"ConsoleApp.Work1", L"DoStuff1", GlobalTypeReferences.System_Void },
    // { L"ConsoleApp.exe", L"ConsoleApp.Work2", L"DoStuff2", GlobalTypeReferences.System_Void },
};
