#include "hardcoded_secrets_method_analyzer.h"
#include "dataflow_il_rewriter.h"
#include "iast_util.h"
#include "method_info.h"
#include "module_info.h"

namespace iast
{
CS _cs;
std::vector<UserString> _userStrings;
std::vector<UserString> _deliveredUserStrings;

HardcodedSecretsMethodAnalyzer::~HardcodedSecretsMethodAnalyzer()
{
}

bool HardcodedSecretsMethodAnalyzer::ProcessMethod(MethodInfo* method)
{
    std::vector<UserString> userStrings;
    ILRewriter* rewriter;
    HRESULT hr = method->GetILRewriter(&rewriter);
    if (SUCCEEDED(hr))
    {
        shared::WSTRING methodName;
        auto module = method->GetModuleInfo();
        bool written = false;
        for (ILInstr* pInstr = rewriter->GetILList()->m_pNext; pInstr != rewriter->GetILList();
             pInstr = pInstr->m_pNext)
        {
            if (pInstr->m_opcode == CEE_LDSTR)
            {
                // Retrieve String
                auto userString = module->GetUserString(pInstr->m_Arg32);
                if (userString.size() < 10)
                {
                    continue;
                }
                if (methodName.size() == 0)
                {
                    methodName = method->GetFullName(false);
                }
                userStrings.push_back({methodName, userString});
            }
        }
        hr = method->CommitILRewriter();
    }

    if (userStrings.size() > 0)
    {
        CSGUARD(_cs);
        for (auto userString : userStrings)
        {
            _userStrings.push_back(userString);
        }
    }

    return false;
}

int HardcodedSecretsMethodAnalyzer::GetUserStrings(int arrSize, UserStringInterop* arr)
{
    CSGUARD(_cs);
    _deliveredUserStrings.clear();
    int x = 0;
    while (x < arrSize && _userStrings.size() > 0)
    {
        _deliveredUserStrings.push_back(_userStrings.back());
        _userStrings.pop_back();
        UserString* str = &(_deliveredUserStrings[x]);
        arr[x].location = str->location.c_str();
        arr[x].value = str->value.c_str();
        x++;
    }

    return x;
}

} // namespace iast