#include "cor_profiler_base.h"
#include "logging.h"

namespace trace {

CorProfilerBase::CorProfilerBase() : ref_count_(0), info_(nullptr) {}

CorProfilerBase::~CorProfilerBase() {
  if (this->info_ != nullptr) {
    this->info_->Release();
    this->info_ = nullptr;
  }
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::Initialize(IUnknown *pICorProfilerInfoUnk) {
  Debug("CorProfiler::Initialize");
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::Shutdown() {
  Debug("CorProfiler::Shutdown");
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::AppDomainCreationStarted(AppDomainID appDomainId) {
  Debug("CorProfiler::AppDomainCreationStarted: ", appDomainId);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::AppDomainCreationFinished(
    AppDomainID appDomainId, HRESULT hrStatus) {
  Debug("CorProfiler::AppDomainCreationFinished: ", appDomainId,
        ", HRESULT: ", hrStatus);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::AppDomainShutdownStarted(AppDomainID appDomainId) {
  Debug("CorProfiler::AppDomainShutdownStarted: ", appDomainId);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::AppDomainShutdownFinished(
    AppDomainID appDomainId, HRESULT hrStatus) {
  Debug("CorProfiler::AppDomainShutdownFinished: ", appDomainId,
        ", HRESULT: ", hrStatus);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::AssemblyLoadStarted(AssemblyID assemblyId) {
  Debug("CorProfiler::AssemblyLoadStarted: ", assemblyId);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::AssemblyLoadFinished(AssemblyID assemblyId, HRESULT hrStatus) {
  Debug("CorProfiler::AssemblyLoadFinished: ", assemblyId,
        ", HRESULT: ", hrStatus);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::AssemblyUnloadStarted(AssemblyID assemblyId) {
  Debug("CorProfiler::AssemblyUnloadStarted: ", assemblyId);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::AssemblyUnloadFinished(
    AssemblyID assemblyId, HRESULT hrStatus) {
  Debug("CorProfiler::AssemblyUnloadFinished: ", assemblyId,
        ", HRESULT: ", hrStatus);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ModuleLoadStarted(ModuleID moduleId) {
  Debug("CorProfiler::ModuleLoadStarted: ", moduleId);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus) {
  Debug("CorProfiler::ModuleLoadFinished: ", moduleId, ", HRESULT: ", hrStatus);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ModuleUnloadStarted(ModuleID moduleId) {
  Debug("CorProfiler::ModuleUnloadStarted: ", moduleId);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus) {
  Debug("CorProfiler::ModuleUnloadFinished: ", moduleId,
        ", HRESULT: ", hrStatus);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ModuleAttachedToAssembly(
    ModuleID moduleId, AssemblyID AssemblyId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ClassLoadStarted(ClassID classId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ClassLoadFinished(ClassID classId,
                                                             HRESULT hrStatus) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ClassUnloadStarted(ClassID classId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ClassUnloadFinished(ClassID classId, HRESULT hrStatus) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::FunctionUnloadStarted(FunctionID functionId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::JITCompilationStarted(
    FunctionID functionId, BOOL fIsSafeToBlock) {
  Debug("CorProfiler::JITCompilationStarted: ", functionId,
        ", fIsSafeToBlock: ", fIsSafeToBlock);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::JITCompilationFinished(
    FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock) {
  Debug("CorProfiler::JITCompilationFinished: ", functionId,
        ", HRESULT: ", hrStatus);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::JITCachedFunctionSearchStarted(
    FunctionID functionId, BOOL *pbUseCachedFunction) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::JITCachedFunctionSearchFinished(
    FunctionID functionId, COR_PRF_JIT_CACHE result) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::JITFunctionPitched(FunctionID functionId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::JITInlining(FunctionID callerId,
                                                       FunctionID calleeId,
                                                       BOOL *pfShouldInline) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ThreadCreated(ThreadID threadId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ThreadDestroyed(ThreadID threadId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ThreadAssignedToOSThread(
    ThreadID managedThreadId, DWORD osThreadId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::RemotingClientInvocationStarted() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::RemotingClientSendingMessage(GUID *pCookie, BOOL fIsAsync) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::RemotingClientReceivingReply(GUID *pCookie, BOOL fIsAsync) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::RemotingClientInvocationFinished() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::RemotingServerReceivingMessage(GUID *pCookie, BOOL fIsAsync) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::RemotingServerInvocationStarted() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::RemotingServerInvocationReturned() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::RemotingServerSendingReply(GUID *pCookie, BOOL fIsAsync) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::UnmanagedToManagedTransition(
    FunctionID functionId, COR_PRF_TRANSITION_REASON reason) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ManagedToUnmanagedTransition(
    FunctionID functionId, COR_PRF_TRANSITION_REASON reason) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::RuntimeSuspendFinished() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::RuntimeSuspendAborted() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::RuntimeResumeStarted() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::RuntimeResumeFinished() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::RuntimeThreadSuspended(ThreadID threadId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::RuntimeThreadResumed(ThreadID threadId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::MovedReferences(
    ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[],
    ObjectID newObjectIDRangeStart[], ULONG cObjectIDRangeLength[]) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ObjectAllocated(ObjectID objectId,
                                                           ClassID classId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ObjectsAllocatedByClass(
    ULONG cClassCount, ClassID classIds[], ULONG cObjects[]) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ObjectReferences(ObjectID objectId, ClassID classId,
                                  ULONG cObjectRefs, ObjectID objectRefIds[]) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::RootReferences(ULONG cRootRefs, ObjectID rootRefIds[]) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ExceptionThrown(ObjectID thrownObjectId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ExceptionSearchFunctionEnter(FunctionID functionId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ExceptionSearchFunctionLeave() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ExceptionSearchFilterEnter(FunctionID functionId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ExceptionSearchFilterLeave() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ExceptionSearchCatcherFound(FunctionID functionId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ExceptionOSHandlerEnter(UINT_PTR __unused) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ExceptionOSHandlerLeave(UINT_PTR __unused) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ExceptionUnwindFunctionEnter(FunctionID functionId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ExceptionUnwindFunctionLeave() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ExceptionUnwindFinallyEnter(FunctionID functionId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ExceptionUnwindFinallyLeave() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ExceptionCatcherEnter(
    FunctionID functionId, ObjectID objectId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ExceptionCatcherLeave() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::COMClassicVTableCreated(
    ClassID wrappedClassId, REFGUID implementedIID, void *pVTable,
    ULONG cSlots) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::COMClassicVTableDestroyed(
    ClassID wrappedClassId, REFGUID implementedIID, void *pVTable) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ExceptionCLRCatcherFound() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ExceptionCLRCatcherExecute() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ThreadNameChanged(ThreadID threadId,
                                                             ULONG cchName,
                                                             WCHAR name[]) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::GarbageCollectionStarted(
    int cGenerations, BOOL generationCollected[], COR_PRF_GC_REASON reason) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::SurvivingReferences(
    ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[],
    ULONG cObjectIDRangeLength[]) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::GarbageCollectionFinished() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::FinalizeableObjectQueued(
    DWORD finalizerFlags, ObjectID objectID) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::RootReferences2(
    ULONG cRootRefs, ObjectID rootRefIds[], COR_PRF_GC_ROOT_KIND rootKinds[],
    COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[]) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::HandleCreated(GCHandleID handleId, ObjectID initialObjectId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::HandleDestroyed(GCHandleID handleId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::InitializeForAttach(
    IUnknown *pCorProfilerInfoUnk, void *pvClientData, UINT cbClientData) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ProfilerAttachComplete() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ProfilerDetachSucceeded() {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ReJITCompilationStarted(
    FunctionID functionId, ReJITID rejitId, BOOL fIsSafeToBlock) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::GetReJITParameters(
    ModuleID moduleId, mdMethodDef methodId,
    ICorProfilerFunctionControl *pFunctionControl) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ReJITCompilationFinished(
    FunctionID functionId, ReJITID rejitId, HRESULT hrStatus,
    BOOL fIsSafeToBlock) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::ReJITError(ModuleID moduleId,
                                                      mdMethodDef methodId,
                                                      FunctionID functionId,
                                                      HRESULT hrStatus) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::MovedReferences2(
    ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[],
    ObjectID newObjectIDRangeStart[], SIZE_T cObjectIDRangeLength[]) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::SurvivingReferences2(
    ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[],
    SIZE_T cObjectIDRangeLength[]) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ConditionalWeakTableElementReferences(ULONG cRootRefs,
                                                       ObjectID keyRefIds[],
                                                       ObjectID valueRefIds[],
                                                       GCHandleID rootIds[]) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::GetAssemblyReferences(
    const WCHAR *wszAssemblyPath,
    ICorProfilerAssemblyReferenceProvider *pAsmRefProvider) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE
CorProfilerBase::ModuleInMemorySymbolsUpdated(ModuleID moduleId) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::DynamicMethodJITCompilationStarted(
    FunctionID functionId, BOOL fIsSafeToBlock, LPCBYTE ilHeader,
    ULONG cbILHeader) {
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfilerBase::DynamicMethodJITCompilationFinished(
    FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock) {
  return S_OK;
}

}  // namespace trace
