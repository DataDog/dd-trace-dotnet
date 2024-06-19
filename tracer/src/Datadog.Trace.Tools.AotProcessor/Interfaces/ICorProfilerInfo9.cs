namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerInfo9 : ICorProfilerInfo8
{
    public static new readonly Guid Guid = new("008170DB-F8CC-4796-9A51-DC8AA0B47012");

    // Given functionId + rejitId, enumerate the native code start address of all jitted versions of this code that currently exist
    HResult GetNativeCodeStartAddresses(FunctionId functionID, ReJITId reJitId, uint cCodeStartAddresses, uint* pcCodeStartAddresses, uint* codeStartAddresses);

    // Given the native code start address, return the native->IL mapping information for this jitted version of the code
    HResult GetILToNativeMapping3(uint* pNativeCodeStartAddress, uint cMap, uint* pcMap, COR_DEBUG_IL_TO_NATIVE_MAP* map);

    // Given the native code start address, return the blocks of virtual memory that store this code (method code is not necessarily stored in a single contiguous memory region)
    HResult GetCodeInfo4(uint* pNativeCodeStartAddress, uint cCodeInfos, uint* pcCodeInfos, COR_PRF_CODE_INFO* codeInfos);
}
