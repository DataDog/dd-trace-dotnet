namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerInfo8 : ICorProfilerInfo7
{
    public static new readonly Guid Guid = new("C5AC80A6-782E-4716-8044-39598C60CFBF");

    /*
    * Determines if a function has associated metadata
    *
    * Certain methods like IL Stubs or LCG Methods do not have
    * associated metadata that can be retrieved using the IMetaDataImport APIs.
    *
    * Such methods can be encountered by profilers through instruction pointers
    * or by listening to ICorProfilerCallback::DynamicMethodJITCompilationStarted
    *
    * This API can be used to determine whether a FunctionID is dynamic.
    */
    HResult IsFunctionDynamic(FunctionId functionId, out int isDynamic);

    /*
    * Maps a managed code instruction pointer to a FunctionID.
    *
    * GetFunctionFromIP2 fails for dynamic methods, this method works for
    * both dynamic and non-dynamic methods. It is a superset of GetFunctionFromIP2
    */
    HResult GetFunctionFromIP3(
        nint ip,
        FunctionId* functionId,
        out ReJITId pReJitId);

    /*
    * Retrieves information about dynamic methods
    *
    * Certain methods like IL Stubs or LCG do not have
    * associated metadata that can be retrieved using the IMetaDataImport APIs.
    *
    * Such methods can be encountered by profilers through instruction pointers
    * or by listening to ICorProfilerCallback::DynamicMethodJITCompilationStarted
    *
    * This API can be used to retrieve information about dynamic methods
    * including a friendly name if available.
    */
    HResult GetDynamicFunctionInfo(
        FunctionId functionId,
        out ModuleId moduleId,
        byte* ppvSig,
        out uint pbSig,
        uint cchName,
        out uint pcchName,
        char* wszName);
}
