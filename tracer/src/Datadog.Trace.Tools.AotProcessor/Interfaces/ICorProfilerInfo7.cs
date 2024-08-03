namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerInfo7 : ICorProfilerInfo6
{
    public static new readonly Guid Guid = new("9AEECC0D-63E0-4187-8C00-E312F503F663");

    /*
    * Applies the newly emitted Metadata.
    *
    * This method can be used to apply the newly defined metadata by IMetadataEmit::Define* methods
    * to the module.
    *
    * If metadata changes are made after ModuleLoadFinished callback,
    * it is required to call this method before using the new metadata
    */
    HResult ApplyMetaData(
        ModuleId moduleId);

    /* Returns the length of an in-memory symbol stream
    *
    * If the module has in-memory symbols the length of the stream will
    * be placed in pCountSymbolBytes. If the module doesn't have in-memory
    * symbols, *pCountSymbolBytes = 0
    *
    * Returns S_OK if the length could be determined (even if it is 0)
    *
    * Note: The current implementation does not support reflection.emit.
    * CORPROF_E_MODULE_IS_DYNAMIC will be returned in that case.
    */
    HResult GetInMemorySymbolsLength(
        ModuleId moduleId,
        out int pCountSymbolBytes);

    /* Reads bytes from an in-memory symbol stream
    *
    * This function attempts to read countSymbolBytes of data starting at offset
    * symbolsReadOffset within the in-memory stream. The data will be copied into
    * pSymbolBytes which is expected to have countSymbolBytes of space available.
    * pCountSymbolsBytesRead contains the actual number of bytes read which
    * may be less than countSymbolBytes if the end of the stream is reached.
    *
    * Returns S_OK if a non-zero number of bytes were read.
    *
    * Note: The current implementation does not support reflection.emit.
    * CORPROF_E_MODULE_IS_DYNAMIC will be returned in that case.
    */
    HResult ReadInMemorySymbols(
        ModuleId moduleId,
        int symbolsReadOffset,
        byte* pSymbolBytes,
        int countSymbolBytes,
        out int pCountSymbolBytesRead);
}
