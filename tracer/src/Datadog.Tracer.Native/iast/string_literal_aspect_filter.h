#pragma once
#include "../../../../shared/src/native-src/pal.h"
#include "aspect_filter.h"
#include "method_info.h"
#include "dataflow_il_rewriter.h"

namespace iast
{
    class StringLiteralsAspectFilter : public AspectFilter
    {
    private:
        std::set<mdMemberRef> _targetMemberRefs;
        HRESULT ResolveTargetMemberRefs();
        bool IsTargetMemberRef(mdMemberRef memberRef);
        bool ComesFromStringLiteral(ILInstr* instruction, ILRewriter* processor);
    public:
        StringLiteralsAspectFilter(ModuleAspects* module, bool any = false);

        bool AllowInstruction(DataflowContext& context) override;
        bool AllowInstruction(DataflowContext& context, const std::vector<int>& indexes, bool any);
    };

    class StringLiteralsAspectFilter_Base : public AspectFilter
    {
    private:
        StringLiteralsAspectFilter* _baseFilter;
        bool _any;
        std::vector<int> _indexes;
    public:
        StringLiteralsAspectFilter_Base(ModuleAspects* module, std::vector<int> indexes, bool any = false);
        StringLiteralsAspectFilter_Base(ModuleAspects* module, int index);

        bool AllowInstruction(DataflowContext& context) override;
    };

    class StringLiterals_AnyAspectFilter : public StringLiteralsAspectFilter_Base
    {
    public:
        StringLiterals_AnyAspectFilter(ModuleAspects* module);
    };
    class StringLiteral_0AspectFilter : public StringLiteralsAspectFilter_Base
    {
    public:
        StringLiteral_0AspectFilter(ModuleAspects* module);
    };
    class StringLiteral_1AspectFilter : public StringLiteralsAspectFilter_Base
    {
    public:
        StringLiteral_1AspectFilter(ModuleAspects* module);
    };
}