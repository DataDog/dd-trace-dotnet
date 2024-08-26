#pragma once
#include "aspect.h"
using namespace shared;

namespace iast
{
    class MethodInfo;
    class MethodSpec;
    class ModuleAspects;
    class Dataflow;
    class AspectFilter;
    class AspectClassReference;
    class DataflowAspect;
    class DataflowAspectReference;
    class ModuleInfo;
    class ILRewriter;
    struct ILInstr;

    struct DataflowContext
    {
        ILRewriter* rewriter;
        ILInstr* instruction;
        bool aborted;
    };

    enum class DataflowAspectFilterValue
    {
        None,
        StringOptimization, //Common string optimizations
        StringLiterals,     //Filter if all params are String Literals
        StringLiterals_Any, //Filter if any pf the params are String Literals
        StringLiteral_0,    //Filter if param0 is String Literal
        StringLiteral_1,    //Filter if param1 is String Literal
    };

    class DataflowAspectClass
    {
    public:
        DataflowAspectClass(Dataflow* dataflow, const WSTRING& aspectsAssembly, const WSTRING& line);

    protected:
        WSTRING _line = EmptyWStr;
        bool _isValid = false;
        mdTypeDef _aspectTypeDef = 0;
    public:
        Dataflow* _dataflow;
        WSTRING _aspectsAssembly;

        std::vector<WSTRING> _assemblies;
        WSTRING _aspectTypeName;
        AspectType _aspectType = AspectType::None;// "PROPAGATION"_W;
        std::vector<VulnerabilityType> _vulnerabilityTypes;
        std::vector<DataflowAspect> _aspects;
        std::vector<DataflowAspectFilterValue> _filters;

        bool IsValid();
        bool IsTargetModule(ModuleInfo* module);

        WSTRING ToString();
    };

    enum class AspectBehavior
    {
        Unknown,
        MethodReplace,
        CtorReplace,
        InsertBefore,
        InsertAfter
    };
    bool IsReplace(AspectBehavior behavior);

    class DataflowAspect
    {
    public:
        DataflowAspect(DataflowAspectClass* aspectClass, const WSTRING& line);

    protected:
        WSTRING _line = EmptyWStr;
        bool _isValid = false;
        bool _isVirtual = false;
        bool _isGeneric = false;

        AspectType _aspectType = AspectType::None;
        std::vector<VulnerabilityType> _vulnerabilityTypes;

        std::vector<WSTRING> _targetMethodAssemblies;
        std::vector<WSTRING> _targetTypeAssemblies;

    public:
        DataflowAspectClass* _aspectClass = nullptr;
        AspectBehavior _behavior;
        WSTRING _aspectMethodName = EmptyWStr;
        WSTRING _aspectMethodParams = EmptyWStr;

        //Target method data (base virtual)
        WSTRING _targetMethodType = EmptyWStr;
        WSTRING _targetMethod = EmptyWStr;
        WSTRING _targetMethodName = EmptyWStr;
        WSTRING _targetMethodParams = EmptyWStr;

        std::vector<int> _paramShift; //Number of parameters to move up in stack before injecting the Aspect
        std::vector<bool> _boxParam;

        //Final type data
        WSTRING _targetType = EmptyWStr;

        //Aspect filters
        std::vector<DataflowAspectFilterValue> _filters;

        bool IsValid();
        bool IsVirtual();
        bool IsGeneric();
        bool IsTargetModule(ModuleInfo* module);

        AspectType GetAspectType();
        std::vector<VulnerabilityType> GetVulnerabilityTypes();
        DataflowAspectReference* GetAspectReference(ModuleAspects* moduleAspects);

        WSTRING ToString();
    };

    class DataflowAspectReference
    {
    public:
        DataflowAspectReference(ModuleAspects* moduleAspects, DataflowAspect* aspect, mdMemberRef targetMethod, mdTypeRef targetType, const std::vector<mdTypeRef>& paramType);

    public:
        DataflowAspect* _aspect = nullptr;
        ModuleAspects* _moduleAspects = nullptr;
        ModuleInfo* _module = nullptr;
        mdMemberRef _targetMethodRef = 0;
        mdTypeRef _targetTypeRef = 0;
        std::vector<mdTypeRef> _targetParamTypeToken;
        std::vector<AspectFilter*> _filters;

    private:
        std::map<mdMethodSpec, mdMethodSpec> _aspectMethodSpecs;
        mdMemberRef _aspectMemberRef = 0;

    private:
        bool IsReinstrumentation(mdMemberRef method);
        bool IsTargetMethod(mdMemberRef method);

    public:
        std::string GetAspectTypeName();
        std::string GetAspectMethodName();
        AspectType GetAspectType();
        std::vector<VulnerabilityType> GetVulnerabilityTypes();

        mdToken GetAspectMemberRef(MethodSpec* methodSpec);

        bool Apply(DataflowContext& context);
        bool ApplyFilters(DataflowContext& context);
    };
}