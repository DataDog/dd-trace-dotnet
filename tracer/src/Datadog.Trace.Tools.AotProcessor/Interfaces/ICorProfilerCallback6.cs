namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerCallback6 : ICorProfilerCallback5
{
    public static new readonly Guid Guid = Guid.Parse("FC13DF4B-4448-4F4F-950C-BA8D19D00C36");

    // CORECLR DEPRECATION WARNING: This callback does not occur on coreclr.
    // Controlled by the COR_PRF_HIGH_ADD_ASSEMBLY_REFERENCES event mask flag.
    // Notifies the profiler of a very early stage in the loading of an Assembly, where the CLR
    // performs an assembly reference closure walk.  This is useful ONLY if the profiler will need
    // to modify the metadata of the Assembly to add AssemblyRefs (later, in ModuleLoadFinished).  In
    // such a case, the profiler should implement this callback as well, to inform the CLR that assembly references
    // will be added once the module has loaded.  This is useful to ensure that assembly sharing decisions
    // made by the CLR during this early stage remain valid even though the profiler plans to modify the metadata
    // assembly references later on.  This can be used to avoid some instances where profiler metadata
    // modifications can cause the SECURITY_E_INCOMPATIBLE_SHARE error to be thrown.
    //
    // The profiler uses the ICorProfilerAssemblyReferenceProvider provided to add assembly references
    // to the CLR assembly reference closure walker.  The ICorProfilerAssemblyReferenceProvider
    // should only be used from within this callback. The profiler will still need to explicitly add assembly
    // references via IMetaDataAssemblyEmit, from within the ModuleLoadFinished callback for the referencing assembly,
    // even though the profiler implements this GetAssemblyReferences callback.  This callback does not result in
    // modified metadata; only in a modified assembly reference closure walk.
    //
    // The profiler should be prepared to receive duplicate calls to this callback for the same assembly,
    // and should respond identically for each such duplicate call (by making the same set of
    // ICorProfilerAssemblyReferenceProvider::AddAssemblyReference calls).
    HResult GetAssemblyReferences(
        char* wszAssemblyPath,
        IntPtr pAsmRefProvider);
}
