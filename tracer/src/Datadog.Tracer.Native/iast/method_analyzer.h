#pragma once
#include "../../../../shared/src/native-src/pal.h"
#include <atomic>
#include <map>
#include <set>
using namespace shared;

namespace iast
{
    class MethodInfo;

    class MethodAnalyzer
    {
    public:
        virtual ~MethodAnalyzer();
        virtual bool ProcessMethod(MethodInfo* method) = 0;
    };
} // namespace iast
