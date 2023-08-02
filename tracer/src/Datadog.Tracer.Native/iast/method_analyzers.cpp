#include "method_analyzers.h"
#include "method_analyzer.h"
#include "iast_util.h"
#include "method_info.h"
#include "module_info.h"
#include "signature_info.h"
#include "signature_types.h"

namespace iast
{
std::vector<MethodAnalyzer*> Analyzers = MethodAnalyzers::InitAnalyzers();

void MethodAnalyzers::ProcessMethod(MethodInfo* method)
{
    for (auto analyzer : Analyzers)
    {
        analyzer->ProcessMethod(method);
    }
}

void MethodAnalyzers::Destroy()
{
    DEL_VEC_VALUES(Analyzers);
}

}