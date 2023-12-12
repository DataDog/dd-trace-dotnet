#pragma once
#include "method_analyzer.h"
#include "../../../../shared/src/native-src/pal.h"
#include <atomic>
#include <map>
#include <set>

#include "hardcoded_secrets_method_analyzer.h"

using namespace shared;

namespace iast
{
    class MethodInfo;

    class MethodAnalyzers
    {
    public:
        inline static std::vector<MethodAnalyzer*> InitAnalyzers()
        {
            std::vector<MethodAnalyzer*> res;
            res.push_back(new HardcodedSecretsMethodAnalyzer());
            return res;
        }

        static void ProcessMethod(MethodInfo* method);
        static void Destroy();
    };

} // namespace iast