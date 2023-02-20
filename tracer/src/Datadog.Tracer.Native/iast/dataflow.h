#pragma once
#include "../../../../shared/src/native-src/pal.h"
#include "iast_util.h"
#include "aspect.h"

namespace iast
{
    class CorProfiler;
    class AppDomainInfo;
    class ModuleInfo;
    class MethodInfo;
    class DataflowAspectClass;
    class DataflowAspect;
    class DataflowAspectReference;
    class ILRewriter;
    struct ILInstr;
    enum class DataflowAspectFilterValue;
    class AspectFilter;
    struct InstrumentResult;

    struct RewriteMethodResponse
    {
        ULONG newMethodBodyLen = 0;
        LPCBYTE newMethodBody = nullptr;
    };

    class ModuleAspects
    {
    public:
        ModuleInfo* _module;
        std::vector<DataflowAspectReference*> _aspects;

    private:
        std::unordered_map<DataflowAspectFilterValue, AspectFilter*> _filters;

    public:
        ModuleAspects(Dataflow* dataflow, ModuleInfo* module);
        virtual ~ModuleAspects();

        AspectFilter* GetFilter(DataflowAspectFilterValue filterValue);
    };

    class Dataflow
    {
        friend class ModuleInfo;
        friend class ModuleAspects;
    public:
        Dataflow(ICorProfilerInfo* profiler);
        virtual ~Dataflow();
    private:
        CS _cs;
        ICorProfilerInfo3* _profiler = nullptr;
        COR_PRF_RUNTIME_TYPE m_runtimeType = COR_PRF_DESKTOP_CLR;
        VersionInfo m_runtimeVersion = VersionInfo{4, 0, 0, 0};

        std::thread* _initThread = nullptr;
        std::map<ModuleID, ModuleInfo*> _modules;
        std::map<AppDomainID, AppDomainInfo*> _appDomains;

        std::vector<WSTRING> _domainIncludeFilters;
        std::vector<WSTRING> _domainExcludeFilters;
        std::vector<WSTRING> _assemblyIncludeFilters;
        std::vector<WSTRING> _assemblyExcludeFilters;
        std::vector<WSTRING> _methodIncludeFilters;
        std::vector<WSTRING> _methodExcludeFilters;

        bool _traceJitMethods = false;

    protected:
        bool _initialized = false;
        bool _loaded = false;

        std::vector<DataflowAspectClass*> _aspectClasses;
        std::vector<DataflowAspect*> _aspects;
        std::map<ModuleID, ModuleAspects*> _moduleAspects;

        HRESULT RewriteMethod(MethodInfo* method, ICorProfilerFunctionControl* pFunctionControl);
        MethodInfo* JITProcessMethod(ModuleID moduleId, mdToken methodId, bool isRejit = false);

        std::vector<DataflowAspectReference*> GetAspects(ModuleInfo* module);
        static InstrumentResult InstrumentInstruction(ILRewriter* rewriter, ILInstr* instruction, std::vector<DataflowAspectReference*>& aspects);

    public:
        bool IsInitialized();

        HRESULT Init();
        HRESULT Destroy();
        HRESULT AppDomainShutdown(AppDomainID appDomainId);
        HRESULT ModuleLoaded(ModuleID moduleId, ModuleInfo** pModuleInfo = nullptr);
        HRESULT ModuleUnloaded(ModuleID moduleId);

        void LoadAspects(WCHAR** aspects, int aspectsLength);

        ICorProfilerInfo* GetCorProfilerInfo();

        HRESULT GetModuleInterfaces(ModuleID moduleID, IMetaDataImport2** ppMetadataImport,
                                    IMetaDataEmit2** ppMetadataEmit, IMetaDataAssemblyImport** ppAssemblyImport,
                                    IMetaDataAssemblyEmit** ppAssemblyEmit);

        bool IsAppDomainExcluded(const WSTRING& appDomainName, MatchResult* includedMatch = nullptr,
                                 MatchResult* excludedMatch = nullptr);
        bool IsAssemblyExcluded(const WSTRING& assemblyName, MatchResult* includedMatch = nullptr,
                                MatchResult* excludedMatch = nullptr);
        bool IsMethodExcluded(const WSTRING& signature, MatchResult* includedMatch = nullptr,
                              MatchResult* excludedMatch = nullptr);

        AppDomainInfo* GetAppDomain(AppDomainID id);
        ModuleInfo* GetModuleInfo(ModuleID moduleId);
        ModuleInfo* GetModuleInfo(WSTRING moduleName, AppDomainID appDomainId, bool lookInSharedRepos = false);

        mdMethodDef GetFunctionInfo(FunctionID functionId, ModuleInfo** ppModuleInfo);

        MethodInfo* GetMethodInfo(ModuleID moduleId, mdMethodDef methodId);
        MethodInfo* GetMethodInfo(FunctionID functionId);

        bool IsInlineEnabled(ModuleID calleeModuleId, mdToken calleeMethodId);
        bool JITCompilationStarted(ModuleID moduleId, mdToken methodId);
    };
}