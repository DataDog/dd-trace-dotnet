#pragma once
#include "../../../../shared/src/native-src/pal.h"
#include "aspect_filter.h"
#include "string_optimization_aspect_filter.h"
#include "string_literal_aspect_filter.h"

namespace iast
{
    class ModuleAspects;

#define DECLARE_ASPECT_FILTER(id, name) if (filterId == DataflowAspectFilterValue::id) { return new name(module); } 

    AspectFilter* GetAspectFilter(DataflowAspectFilterValue filterId, ModuleAspects* module)
    {
        if (filterId == DataflowAspectFilterValue::None) { return nullptr; }
        DECLARE_ASPECT_FILTER(StringOptimization, StringOptimizationAspectFilter);  //Common string optimizations
        DECLARE_ASPECT_FILTER(StringLiterals, StringLiteralsAspectFilter);          //Filter if all params are String Literals
        DECLARE_ASPECT_FILTER(StringLiterals_Any, StringLiterals_AnyAspectFilter);  //Filter if all params are String Literals
        DECLARE_ASPECT_FILTER(StringLiteral_0, StringLiteral_0AspectFilter);        //Filter if param0 is String Literal
        DECLARE_ASPECT_FILTER(StringLiteral_1, StringLiteral_1AspectFilter);        //Filter if param1 is String Literal
        return nullptr;
    }
}