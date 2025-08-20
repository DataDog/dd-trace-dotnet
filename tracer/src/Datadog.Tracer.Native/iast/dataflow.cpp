#include "dataflow.h"
#include "iast_util.h"
#include "module_info.h"
#include "method_info.h"
#include "method_analyzers.h"
#include "dataflow_aspects.h"
#include "dataflow_il_rewriter.h"
#include "aspect_filter_factory.h"
#include "../function_control_wrapper.h"
#include <fstream>
#include <chrono>
#include "../../../../shared/src/native-src/com_ptr.h"
#include "../environment_variables_util.h"

using namespace std::chrono;

namespace iast
{
static std::vector<WSTRING> _domainIncludeFilters = {
};
static std::vector<WSTRING> _domainExcludeFilters = {
    WStr("DD*"), 
    WStr("DataDog*"),
};
static std::vector<WSTRING> _assemblyIncludeFilters = {
};
static std::vector<WSTRING> _assemblyExcludeFilters = {
    WStr("System*"),
    WStr("Datadog.*"),
    WStr("Kudu*"),
    WStr("Microsoft*"),
    WStr("MSBuild"),
    WStr("dotnet"),
    WStr("netstandard"),
    WStr("AspNet.*"),
    WStr("msvcm90*"),
    WStr("Mono.*"),
    WStr("NuGet.*"),
    WStr("PCRE.*"),
    WStr("Antlr*"),
    WStr("Azure.Messaging.ServiceBus*"),
    WStr("PostSharp"),
    WStr("SMDiagnostics"),
    WStr("testhost"),
    WStr("WebGrease"),
    WStr("YamlDotNet"),
    WStr("EnvSettings*"),
    WStr("EntityFramework*"),
    WStr("linq2db*"),
    WStr("Newtonsoft.Json*"),
    WStr("log4net*"),
    WStr("Autofac*"),
    WStr("StackExchange*"),
    WStr("BundleTransformer*"),
    WStr("LibSassHost*"),
    WStr("ClearScript*"),
    WStr("NewRelic*"),
    WStr("AppDynamics*"),
    WStr("NProfiler*"),
    WStr("KTJdotnetTls*"),
    WStr("KTJUniDC*"),
    WStr("Dynatrace*"),
    WStr("oneagent*"),
    WStr("CommandLine"),
    WStr("Moq"),
    WStr("Castle.Core"),
    WStr("MiniProfiler*"),
    WStr("MySql*"),
    WStr("Serilog*"),
    WStr("ServiceStack*"),
    WStr("mscorlib"),
    WStr("Xunit.*"),
    WStr("xunit.*"),
    WStr("FluentAssertions"),
    WStr("NUnit3.TestAdapter"),
    WStr("nunit.*"),
    WStr("vstest.console"),
    WStr("testhost.*"),
    WStr("Oracle.ManagedDataAccess"),
    WStr("DelegateDecompiler*"),
    WStr("FluentValidation*"),
    WStr("NHibernate*"),
    WStr("Npgsql*"),
    WStr("Grpc.Net.Client"),
    WStr("Amazon.Runtime*"),
    WStr("App.Metrics.Concurrency*"),
    WStr("AWSSDK.SimpleEmail"),
    WStr("AWSSDK.Core"),
    WStr("MailKit"),
    WStr("MimeKit"),
};
static std::vector<WSTRING> _methodIncludeFilters = {
    WStr("System.Web.Mvc.ControllerActionInvoker::InvokeAction*"),
    WStr("System.Web.Mvc.Async.AsyncControllerActionInvoker*"),
    WStr("System.Web.Http.Controllers.ReflectedHttpActionDescriptor::ExecuteAsync*"),
    WStr("System.Net.Http.HttpRequestMessage*"),
    WStr("System.ServiceModel.Dispatcher*"),
    WStr("MongoDB.Bson.Serialization.Serializers.StringSerializer*"),
};
static std::vector<WSTRING> _methodExcludeFilters = {
    WStr("DataDog*"),
    WStr("System.Web.Mvc*"),
    WStr("System.Web.PrefixContainer*"),
    WStr("Microsoft.ClearScript*"),
    WStr("JavaScriptEngineSwitcher*"),
    WStr("IBM.Tivoli*"),
    WStr("Dynatrace*"),
    WStr("Microsoft.AspNetCore.Razor.Tools*"),
    WStr("Microsoft.Extensions.CommandLineUtils*"),
    WStr("System.Net.Http*"),
    WStr("System.ServiceModel*"),
    WStr("System.Web.Http*"),
    WStr("MongoDB.*"),
    WStr("JetBrains*"),
    WStr("RestSharp.Extensions.StringExtensions::UrlEncode*"),
};
static std::vector<WSTRING> _methodAttributeIncludeFilters = {
    WStr("DelegateDecompiler.ComputedAttribute"),
};
static std::vector<WSTRING> _methodAttributeExcludeFilters = {
};

ModuleAspects::ModuleAspects(Dataflow* dataflow, ModuleInfo* module)
{
    this->_module = module;

    // Determine aspects which apply to this module
    for (auto const& a : dataflow->_aspects)
    {
        auto aspectReference = a->GetAspectReference(this);
        if (aspectReference)
        {
            _aspects.push_back(aspectReference);
        }
    }
}
ModuleAspects::~ModuleAspects()
{
    DEL_MAP_VALUES(_filters);
    DEL_VEC_VALUES(_aspects);
}
AspectFilter* ModuleAspects::GetFilter(DataflowAspectFilterValue filterValue)
{
    if (filterValue == DataflowAspectFilterValue::None)
    {
        return nullptr;
    }
    auto value = _filters.find(filterValue);
    if (value != _filters.end())
    {
        return value->second;
    }
    auto res = GetAspectFilter(filterValue, this);
    _filters[filterValue] = res;
    return res;
}

//--------------------

Dataflow::Dataflow(ICorProfilerInfo* profiler, std::shared_ptr<RejitHandler> rejitHandler,
                   const RuntimeInformation& runtimeInfo) :
    Rejitter(rejitHandler, RejitterPriority::Low)
{
    m_runtimeType = runtimeInfo.runtime_type;
    m_runtimeVersion = VersionInfo{runtimeInfo.major_version, runtimeInfo.minor_version, runtimeInfo.build_version, 0};
    trace::Logger::Info("Dataflow::Dataflow -> Detected runtime version : ", m_runtimeVersion.ToString());

    this->_setILOnJit = trace::IsEditAndContinueEnabled();
    if (this->_setILOnJit)
    {
        trace::Logger::Info("Dataflow detected Edit and Continue feature (COMPLUS_ForceEnc != 0) : Enabling SetILCode in JIT event.");
    }

    HRESULT hr = profiler->QueryInterface(__uuidof(ICorProfilerInfo3), (void**) &_profiler);
    if (FAILED(hr))
    {
        _profiler = nullptr;
        trace::Logger::Error("Dataflow::Dataflow -> Something very wrong happened, as QI on ICorProfilerInfo3 failed. Disabling Dataflow. HRESULT : ", Hex(hr));
    }
}
Dataflow::~Dataflow()
{
    Destroy();
}

void Dataflow::Destroy()
{
    if (_initialized)
    {
        _initialized = false;
        REL(_profiler);
        DEL_MAP_VALUES(_modules);
        DEL_MAP_VALUES(_appDomains);
        DEL_MAP_VALUES(_moduleAspects);
    }
}

void Dataflow::LoadAspects(WCHAR** aspects, int aspectsLength, UINT32 enabledCategories, UINT32 platform)
{
    // Init aspects
    DBG("Dataflow::LoadAspects -> Processing aspects... ", aspectsLength, " ", enabledCategories, " ", platform);

    DataflowAspectClass* aspectClass = nullptr;
    for (int x = 0; x < aspectsLength; x++)
    {
        WSTRING line = aspects[x];
        if (BeginsWith(line, WStr("[AspectClass(")))
        {
            aspectClass = new DataflowAspectClass(this, line, enabledCategories);
            if (!aspectClass->IsValid())
            {
                DBG("Dataflow::LoadAspects -> Detected invalid aspect class ", line);
                DEL(aspectClass);
            }
            else
            {
                _aspectClasses.push_back(aspectClass);
            }
            continue;
        }
        if (BeginsWith(line, WStr("  [Aspect")) && aspectClass != nullptr)
        {
            auto aspect = new DataflowAspect(aspectClass, line, platform);
            if (!aspect->IsValid())
            {
                DBG("Dataflow::LoadAspects -> Detected invalid aspect ", line);
                DEL(aspect);
            }
            else
            {
                _aspects.push_back(aspect);
            }
        }
    }

    LoadSecurityControls();

    auto moduleAspects = _moduleAspects;
    _moduleAspects.clear();
    DEL_MAP_VALUES(moduleAspects);

    trace::Logger::Info("Dataflow::LoadAspects -> read ", _aspects.size(), " aspects");
    _loaded = true;
}

void Dataflow::LoadSecurityControls()
{
    auto securityControlsConfig = shared::GetEnvironmentValue(environment::security_controls_configuration);
    if (!securityControlsConfig.empty())
    {
        DataflowAspectClass* aspectClass = nullptr;

        DBG("Dataflow::LoadSecurityControls -> Processing Security Controls Config... ", securityControlsConfig);
        auto securityControls = shared::Split(securityControlsConfig, ';');
        for (auto const& securityControlLine : securityControls)
        {
            auto securityControl = shared::Trim(securityControlLine);
            if (securityControl.size() == 0 || securityControl[0] == '#')
            {
                continue;
            }

            auto parts = shared::Split(securityControl, ':');
            if (parts.size() < 5)
            {
                trace::Logger::Warn("Dataflow::LoadSecurityControls -> Detected invalid Security Control: ",
                                    securityControl);
                continue;
            }

            int part = -1;
            SecurityControlType securityControlType = SecurityControlType::Unknown;
            if ((int) parts.size() > ++part) // Security control kind
            {
                securityControlType = ParseSecurityControlType(parts[part]);
            }
            if (securityControlType == SecurityControlType::Unknown)
            {
                trace::Logger::Warn("Dataflow::LoadSecurityControls -> Detected invalid Security Control type: ",
                                    parts[part], " in ",
                                    securityControl);
                continue;
            }

            UINT32 secureMarks = 0;
            if ((int) parts.size() > ++part) // Vulnerability type
            {
                for (auto const& vulnPart : Split(shared::ToString(parts[part]), ","))
                {
                    auto vuln = ParseVulnerabilityType(shared::ToString(vulnPart));
                    if (vuln == VulnerabilityType::None)
                    {
                        trace::Logger::Warn(
                            "Dataflow::LoadSecurityControls -> Detected invalid Security Control vulnerability type: ",
                            vulnPart, " in ",
                            securityControl);
                        continue;
                    }

                    secureMarks |= (UINT32) vuln;
                }
            }
            if (secureMarks == 0)
            {
                trace::Logger::Warn(
                    "Dataflow::LoadSecurityControls -> Detected invalid Security Control vulnerability types: ",
                    securityControl);
                continue;
            }

            auto targetAssembly = parts[++part];
            auto targetType = parts[++part];
            auto targetMethodPart = parts[++part];

            std::vector<int> parameterIndexes(5);
            if ((int) parts.size() > ++part) // Parameter indexes
            {
                for (auto const& paramPart : Split(shared::ToString(parts[part]), ","))
                {
                    int param = -1;
                    if (!TryParseInt(paramPart, &param))
                    {
                        trace::Logger::Warn(
                            "Dataflow::LoadSecurityControls -> Detected invalid Security Control parameter index: ",
                            paramPart, " in ",
                            securityControl);
                        continue;
                    }
                    parameterIndexes.push_back(param);
                }
            }

            if (parameterIndexes.empty())
            {
                parameterIndexes.push_back(0);
            }

            WSTRING targetMethod, targetParams;
            SplitType(targetMethodPart, nullptr, nullptr, &targetMethod, &targetParams);

            if (aspectClass == nullptr)
            {
                aspectClass = new SecurityControlAspectClass(this);
                _aspectClasses.push_back(aspectClass);
                DBG("Dataflow::LoadSecurityControls -> Created AspectClass");
            }

            auto aspect = new SecurityControlAspect(aspectClass, secureMarks, securityControlType, targetAssembly,
                                                    targetType, targetMethod, targetParams, parameterIndexes);

            if (Logger::IsDebugEnabled())
            {
                auto params = iast::Join(parameterIndexes, ",");
                Logger::Debug("Dataflow::LoadSecurityControls -> Created Aspect: ", (int) securityControlType,
                              "  ", targetAssembly, " | ", targetType, " :: ", targetMethod, "  ", targetParams,
                              " [", params, "]");
            }

            _aspects.push_back(aspect);
        }

        DBG("Dataflow::LoadSecurityControls -> Exit");
    }
}


HRESULT Dataflow::AppDomainShutdown(AppDomainID appDomainId)
{
    CSGUARD(_cs);
    auto it = _appDomains.find(appDomainId);
    if (it != _appDomains.end())
    {
        DBG("AppDomainShutdown: AppDomainId = ", Hex((ULONG) appDomainId), " [ ", it->second->Name, " ] ");
        DEL(it->second);
        _appDomains.erase(appDomainId);
        return S_OK;
    }
    return S_FALSE;
}

HRESULT Dataflow::ModuleLoaded(ModuleID moduleId, ModuleInfo** pModuleInfo)
{
    LPCBYTE pbBaseLoadAddr;
    WCHAR wszPath[300];
    ULONG cchNameIn = 300;
    ULONG cchNameOut;
    AssemblyID assemblyId;
    AppDomainID appDomainId;
    ModuleID modIDDummy;
    WCHAR wszName[1024];

    DWORD dwModuleFlags;
    HRESULT hr = _profiler->GetModuleInfo2(moduleId, &pbBaseLoadAddr, cchNameIn, &cchNameOut, wszPath, &assemblyId,
                                           &dwModuleFlags);
    if (FAILED(hr))
    {
        trace::Logger::Error("GetModuleInfo2 failed for ModuleId ", moduleId);
        return hr;
    }
    if ((dwModuleFlags & COR_PRF_MODULE_WINDOWS_RUNTIME) != 0)
    {
        return S_OK;
    } // Ignore any Windows Runtime modules.  We cannot obtain writeable metadata interfaces on them or instrument their
      // IL

    hr = _profiler->GetAssemblyInfo(assemblyId, 1024, nullptr, wszName, &appDomainId, &modIDDummy);
    if (FAILED(hr))
    {
        trace::Logger::Error("GetAssemblyInfo failed for ModuleId ", moduleId, " AssemblyId ", assemblyId);
        return hr;
    }

    AppDomainInfo* appDomain = GetAppDomain(appDomainId);
    if (appDomain == nullptr)
    {
        trace::Logger::Error("GetAppDomain failed for AppDomainId ", appDomainId);
        return E_FAIL;
    }

    WSTRING moduleName = WSTRING(wszName);
    WSTRING modulePath = WSTRING(wszPath);
    ModuleInfo* moduleInfo = new ModuleInfo(this, appDomain, moduleId, modulePath, assemblyId, moduleName);
    DBG("Dataflow::ModuleLoaded -> Loaded Module ", shared::ToString(moduleInfo->GetModuleFullName()));

    CSGUARD(_cs);
    _modules[moduleId] = moduleInfo;
    if (pModuleInfo)
    {
        *pModuleInfo = moduleInfo;
    }
    return S_OK;
}

HRESULT Dataflow::ModuleUnloaded(ModuleID moduleId)
{
    CSGUARD(_cs);
    {
        auto it = _moduleAspects.find(moduleId);
        if (it != _moduleAspects.end())
        {
            auto moduleAspects = it->second;
            DEL(moduleAspects);
        }
        _moduleAspects.erase(moduleId);
    }
    {
        auto it = _modules.find(moduleId);
        if (it != _modules.end())
        {
            DBG("ModuleUnloadFinished: ModuleID = ", Hex((ULONG) moduleId), " [ ", it->second->_appDomain.Name, " ] ", it->second->_name);
            DEL(it->second);
        }
        else
        {
            DBG("ModuleUnloadFinished: ModuleID = ", Hex((ULONG) moduleId), " (Not found)");
        }
        _modules.erase(moduleId);
    }

    return S_OK;
}

HRESULT Dataflow::GetModuleInterfaces(ModuleID moduleId, IMetaDataImport2** ppMetadataImport,
                                      IMetaDataEmit2** ppMetadataEmit, IMetaDataAssemblyImport** ppAssemblyImport,
                                      IMetaDataAssemblyEmit** ppAssemblyEmit)
{
    HRESULT hr = S_OK;
    if (hr == S_OK)
    {
        IUnknown* piUnk = nullptr;
        hr = _profiler->GetModuleMetaData(moduleId, ofRead | ofWrite, IID_IMetaDataImport2, &piUnk);
        if (hr == S_OK)
        {
            hr = piUnk->QueryInterface(IID_IMetaDataImport2, (void**) ppMetadataImport);
            REL(piUnk);
        }
    }
    if (hr == S_OK)
    {
        IUnknown* piUnk = nullptr;
        hr = _profiler->GetModuleMetaData(moduleId, ofRead | ofWrite, IID_IMetaDataEmit2, &piUnk);
        if (hr == S_OK)
        {
            hr = piUnk->QueryInterface(IID_IMetaDataEmit2, (void**) ppMetadataEmit);
            REL(piUnk);
        }
    }
    if (hr == S_OK)
    {
        IUnknown* piUnk = nullptr;
        hr = _profiler->GetModuleMetaData(moduleId, ofRead | ofWrite, IID_IMetaDataAssemblyImport, &piUnk);
        if (hr == S_OK)
        {
            hr = piUnk->QueryInterface(IID_IMetaDataAssemblyImport, (void**) ppAssemblyImport);
            REL(piUnk);
        }
    }
    if (hr == S_OK)
    {
        IUnknown* piUnk = nullptr;
        hr = _profiler->GetModuleMetaData(moduleId, ofRead | ofWrite, IID_IMetaDataAssemblyEmit, &piUnk);
        if (hr == S_OK)
        {
            hr = piUnk->QueryInterface(IID_IMetaDataAssemblyEmit, (void**) ppAssemblyEmit);
            REL(piUnk);
        }
    }
    return hr;
}

bool Dataflow::IsAppDomainExcluded(const WSTRING& appDomainName, MatchResult* includedMatch, MatchResult* excludedMatch)
{
    return IsExcluded(_domainIncludeFilters, _domainExcludeFilters, appDomainName, includedMatch, excludedMatch);
}
bool Dataflow::IsAssemblyExcluded(const WSTRING& assemblyName, MatchResult* includedMatch, MatchResult* excludedMatch)
{
    return IsExcluded(_assemblyIncludeFilters, _assemblyExcludeFilters, assemblyName, includedMatch, excludedMatch);
}
bool Dataflow::IsMethodExcluded(const WSTRING& methodSignature, MatchResult* includedMatch, MatchResult* excludedMatch)
{
    return IsExcluded(_methodIncludeFilters, _methodExcludeFilters, methodSignature, includedMatch, excludedMatch);
}
bool Dataflow::IsMethodAttributeExcluded(const WSTRING& attributeName, MatchResult* includedMatch,
                                         MatchResult* excludedMatch)
{
    return IsExcluded(_methodAttributeIncludeFilters, _methodAttributeExcludeFilters, attributeName, includedMatch,
                      excludedMatch);
}
bool Dataflow::HasMethodAttributeExclusions()
{
    return _methodAttributeExcludeFilters.size() > 0;
}

ICorProfilerInfo* Dataflow::GetCorProfilerInfo()
{
    return _profiler;
}

AppDomainInfo* Dataflow::GetAppDomain(AppDomainID id)
{
    CSGUARD(_cs);
    auto found = _appDomains.find(id);
    if (found == _appDomains.end())
    {
        HRESULT hr = S_OK;
        WCHAR wszAppDomainName[256];
        ULONG cchAppDomainName;
        ProcessID pProcID;
        BOOL fShared = FALSE;

        hr = _profiler->GetAppDomainInfo(id, 256, &cchAppDomainName, wszAppDomainName, &pProcID);
        AppDomainInfo* info = new AppDomainInfo(id, wszAppDomainName, IsAppDomainExcluded(wszAppDomainName));
        _appDomains[id] = info;

        found = _appDomains.find(id);
    }
    if (found != _appDomains.end())
    {
        return found->second;
    }
    return nullptr;
}
ModuleInfo* Dataflow::GetModuleInfo(ModuleID id)
{
    CSGUARD(_cs);
    auto found = _modules.find(id);
    if (found != _modules.end())
    {
        return found->second;
    }
    return nullptr;
}
ModuleInfo* Dataflow::GetModuleInfo(WSTRING moduleName, AppDomainID appDomainId, bool lookInSharedRepos)
{
    CSGUARD(_cs);
    for (auto iterator = _modules.begin(); iterator != _modules.end(); iterator++)
    {
        if (iterator->second->_name == moduleName)
        {
            auto ppModuleInfo = iterator->second;

            if ((ppModuleInfo->_appDomain.Id == appDomainId) ||
                (lookInSharedRepos && ppModuleInfo->_appDomain.IsSharedAssemblyRepository))
            {
                return ppModuleInfo;
            }
        }
    }

    return nullptr;
}
mdMethodDef Dataflow::GetFunctionInfo(FunctionID functionId, ModuleInfo** ppModuleInfo)
{
    HRESULT hr = S_OK;
    ModuleID moduleId;
    *ppModuleInfo = nullptr;
    mdMethodDef methodDef = 0;
    if (SUCCEEDED(_profiler->GetFunctionInfo(functionId, nullptr, &moduleId, &methodDef)))
    {
        if (ppModuleInfo)
        {
            *ppModuleInfo = GetModuleInfo(moduleId);
        }
    }
    return methodDef;
}

MethodInfo* Dataflow::GetMethodInfo(ModuleID moduleId, mdMethodDef methodId)
{
    auto module = GetModuleInfo(moduleId);
    if (module)
    {
        return module->GetMethodInfo(methodId);
    }
    return nullptr;
}
MethodInfo* Dataflow::GetMethodInfo(FunctionID functionId)
{
    HRESULT hr = S_OK;
    ModuleInfo* pModuleInfo;
    mdMethodDef methodDef = GetFunctionInfo(functionId, &pModuleInfo);
    if (pModuleInfo)
    {
        return pModuleInfo->GetMethodInfo(methodDef);
    }
    return nullptr;
}

bool Dataflow::IsInlineEnabled(ModuleID calleeModuleId, mdToken calleeMethodId)
{
    auto method = JITProcessMethod(calleeModuleId, calleeMethodId);
    if (method)
    {
        return method->IsInlineEnabled();
    }
    return true;
}
bool Dataflow::JITCompilationStarted(ModuleID moduleId, mdToken methodId)
{
    if (!_loaded)
    {
        return false;
    }

    auto method = JITProcessMethod(moduleId, methodId);
    return method != nullptr;
}
MethodInfo* Dataflow::JITProcessMethod(ModuleID moduleId, mdToken methodId, trace::FunctionControlWrapper* pFunctionControl)
{
    MethodInfo* method = nullptr;
    if (!_loaded)
    {
        return method;
    }

    auto module = GetModuleInfo(moduleId);
    if (module && !module->IsExcluded())
    {
        method = module->GetMethodInfo(methodId);
        if (method && !method->IsExcluded())
        {
            if (pFunctionControl || !method->IsProcessed())
            {
                method->SetProcessed();
                RewriteMethod(method, pFunctionControl);
            }
        }
    }
    return method;
}

bool IsCandidate(unsigned opcode)
{
    return (opcode == CEE_CALL || opcode == CEE_CALLI || opcode == CEE_CALLVIRT || opcode == CEE_NEWOBJ);
}

HRESULT SetILFunctionBody(MethodInfo* method, ICorProfilerFunctionControl* pFunctionControl, ULONG size, LPCBYTE pBody)
{
    return method->SetMethodIL(size, pBody, pFunctionControl);
}
HRESULT Dataflow::RewriteMethod(MethodInfo* method, trace::FunctionControlWrapper* pFunctionControl)
{
    HRESULT hr = S_OK;

    CSGUARD(_cs);

    if (!pFunctionControl)
    {
        MethodAnalyzers::ProcessMethod(method);
    }

    auto module = method->GetModuleInfo();
    auto moduleAspectRefs = GetAspects(module);
    if (moduleAspectRefs.size() > 0)
    {
        bool written = false;
        ILRewriter* rewriter;
        hr = method->GetILRewriter(&rewriter, (ICorProfilerInfo*) pFunctionControl);
        if (SUCCEEDED(hr))
        {
            DataflowContext context = {rewriter, rewriter->GetILList()->m_pNext, false};

            for (; context.instruction != rewriter->GetILList(); context.instruction = context.instruction->m_pNext)
            {
                if (IsCandidate(context.instruction->m_opcode))
                {
                    // Instrument instruction
                    written |= InstrumentInstruction(context, moduleAspectRefs);
                    if (context.aborted)
                    {
                        break;
                    }
                }
            }
            if (!pFunctionControl && written && !_setILOnJit)
            {
                // We are in JIT event. If _setILOnJit is false, we abort the commit and request a rejit
                context.aborted = true;
            }
            method->SetInstrumented(written);
            method->CommitILRewriter(context.aborted);
        }

        if (written)
        {
            if (pFunctionControl)
            {
                hr = method->ApplyFinalInstrumentation((ICorProfilerFunctionControl*) pFunctionControl);
            }
            else if (_setILOnJit)
            {
                hr = method->ApplyFinalInstrumentation();
            }
            else
            {
                std::vector<ModuleID> modulesVector = {module->_id};
                std::vector<mdMethodDef> methodsVector = {method->GetMemberId()}; // methodId
                DBG("RewriteMethod: REJIT requested for ", method->GetKey());
                m_rejitHandler->RequestRejit(modulesVector, methodsVector);
            }
        }
    }

    return hr;
}

std::vector<DataflowAspectReference*> Dataflow::GetAspects(ModuleInfo* module)
{
    auto value = _moduleAspects.find(module->_id);
    if (value != _moduleAspects.end())
    {
        return value->second->_aspects;
    }
    auto res = new ModuleAspects(this, module);
    _moduleAspects[module->_id] = res;
    return res->_aspects;
}

bool Dataflow::InstrumentInstruction(DataflowContext& context, std::vector<DataflowAspectReference*>& aspects)
{
    for (auto const& aspect : aspects)
    {
        if (aspect->Apply(context))
        {
            return true;
        }
    }
    return false;
}

void Dataflow::Shutdown()
{
    Destroy();
}
RejitHandlerModule* Dataflow::GetOrAddModule(ModuleID moduleId)
{
    return nullptr;
}
bool Dataflow::HasModuleAndMethod(ModuleID moduleId, mdMethodDef methodDef)
{
    return false;
}
void Dataflow::RemoveModule(ModuleID moduleId)
{
}
void Dataflow::AddNGenInlinerModule(ModuleID moduleId)
{
}

HRESULT Dataflow::RejitMethod(trace::FunctionControlWrapper& functionControl)
{
    auto method = JITProcessMethod(functionControl.GetModuleId(), functionControl.GetMethodId(), &functionControl);
    if (method && method->IsWritten())
    {
        return S_OK;
    }
    return S_FALSE;
}


} // namespace iast
