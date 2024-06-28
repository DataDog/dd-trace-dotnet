#pragma once
#include "../../../../shared/src/native-src/pal.h"
#include "aspect_filter.h"
#include "dataflow.h"
#include "dataflow_il_rewriter.h"

namespace iast 
{
	class StringOptimizationAspectFilter : public AspectFilter
	{
	private:
 
		/*
		* How far are we going to search for `CALL target` in the next IL instructions and it is 3 because there are target methods accept 2 arguments as max plus the CALL instruction.
		*
		*   System.String Hdiv.AST.Aspects.StringAspect::ToLower_Track(System.String,System.Int32) Original: [0x0A0000F7] System.String System.String::ToLower()
		*	ldstr 0x700049CA
		*	ldc.i4.3 
		*	callvirt [0x0A000117] System.Boolean System.String::StartsWith(System.String,System.StringComparison)
		*/
		const static int _stackLength = 3;

		std::set<mdMemberRef> _targetMemberRefs;
		HRESULT ResolveTargetMemberRefs();

		bool IsTargetMemberRef(mdMemberRef memberRef);
		static bool IsStLoc(int opcode);
		static bool IsCall(int opcode);

	public:
		StringOptimizationAspectFilter(ModuleAspects* module);
		~StringOptimizationAspectFilter() override;

		bool AllowInstruction(DataflowContext& context) override;
	
	};
}
