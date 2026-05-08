#pragma once
#include "method_analyzer.h"
#include "../../../../shared/src/native-src/pal.h"
#include <atomic>
#include <map>
#include <set>

using namespace shared;

namespace iast
{
    class MethodInfo;

    class MethodAnalyzers
    {
    public:
        inline static std::vector<MethodAnalyzer*> InitAnalyzers()
        {
            return {};
        }

        static void ProcessMethod(MethodInfo* method);
        static void Destroy();
    };

} // namespace iast