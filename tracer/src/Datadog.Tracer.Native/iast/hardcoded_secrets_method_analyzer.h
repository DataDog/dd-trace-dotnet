#pragma once
#include "../../../../shared/src/native-src/pal.h"
#include "method_analyzer.h"
#include <atomic>
#include <map>
#include <set>
#include "method_analyzer.h"
#include "iast_util.h"

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
    private:
        CS _cs;
        std::vector<UserString> _userStrings;
        std::vector<UserString> _deliveredUserStrings;
    public:
        static HardcodedSecretsMethodAnalyzer* Instance;

        HardcodedSecretsMethodAnalyzer();
        virtual ~HardcodedSecretsMethodAnalyzer();

        bool ProcessMethod(MethodInfo* method) override;
        int GetUserStrings(int arrSize, UserStringInterop* arr);
    };

} // namespace iast