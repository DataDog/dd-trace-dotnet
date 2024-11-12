#pragma once
#include "../../../../shared/src/native-src/pal.h"
#include "dataflow_il_rewriter.h"
#include "dataflow.h"

namespace iast
{
	class AspectFilter 
	{
	protected:
		AspectFilter(ModuleAspects * module);
		ModuleAspects* _module;
	public:
		virtual ~AspectFilter();
        virtual bool AllowInstruction(DataflowContext& context) = 0;
	};

	struct AspectFilterTarget
	{
	public:
		std::vector<WSTRING> _assembly;
		WSTRING _typeName;
		std::vector<WSTRING> _methods;

		AspectFilterTarget(const WSTRING& assembly, const WSTRING& typeName, const WSTRING& methods);
		std::set<mdMemberRef> Resolve(ModuleInfo* module);
	};
}