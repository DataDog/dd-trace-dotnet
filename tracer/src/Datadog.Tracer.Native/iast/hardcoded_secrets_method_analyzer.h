#pragma once
#include "../../../../shared/src/native-src/pal.h"
#include "method_analyzer.h"
#include <atomic>
#include <map>
#include <set>
#include "method_analyzer.h"

using namespace shared;

namespace iast
{
    struct UserString
    {
        shared::WSTRING location;
        shared::WSTRING value;
    };

    struct UserStringInterop
    {
        const WCHAR* location;
        const WCHAR* value;
    };

    class HardcodedSecretsMethodAnalyzer : public MethodAnalyzer
    {
    public:
        virtual ~HardcodedSecretsMethodAnalyzer();
        virtual bool ProcessMethod(MethodInfo* method) override;
        static int GetUserStrings(int arrSize, UserStringInterop* arr);
    };

} // namespace iast