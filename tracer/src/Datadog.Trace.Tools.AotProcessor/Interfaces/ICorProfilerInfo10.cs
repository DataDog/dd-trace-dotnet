namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerInfo10 : ICorProfilerInfo9
{
    public static new readonly Guid Guid = new("2F1B5152-C869-40C9-AA5F-3ABE026BD720");

    // Given an ObjectID, callback and clientData, enumerates each object reference (if any).
    HResult EnumerateObjectReferences(ObjectId objectId, delegate* unmanaged<ObjectId, ObjectId*, void*, int> callback, void* clientData);

    // Given an ObjectID, determines whether it is in a read only segment.
    HResult IsFrozenObject(ObjectId objectId, out int pbFrozen);

    // Gets the value of the configured LOH Threshold.
    HResult GetLOHObjectSizeThreshold(out int pThreshold);

    /*
     * This method will ReJIT the methods requested, as well as any inliners
     * of the methods requested.
     *
     * RequestReJIT does not do any tracking of inlined methods. The profiler
     * was expected to track inlining and call RequestReJIT for all inliners
     * to make sure every instance of an inlined method was ReJITted.
     * This poses a problem with ReJIT on attach, since the profiler was
     * not present to monitor inlining. This method can be called to guarantee
     * that the full set of inliners will be ReJITted as well.
     */
    HResult RequestReJITWithInliners(
        int dwRejitFlags,
        uint cFunctions,
        ModuleId* moduleIds,
        MdMethodDef* methodIds);

    // Suspend the runtime without performing a GC.
    HResult SuspendRuntime();

    // Restart the runtime from a previous suspension.
    HResult ResumeRuntime();
}
