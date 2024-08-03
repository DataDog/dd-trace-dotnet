namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerCallback5 : ICorProfilerCallback4
{
    public static new readonly Guid Guid = Guid.Parse("8DFBA405-8C9F-45F8-BFFA-83B14CEF78B5");

    /*
     * The CLR calls ConditionalWeakTableElementReferences with information
     * about dependent handles after a garbage collection has occurred.
     *
     * For each root ID in rootIds, keyRefIds will contain the ObjectID for
     * the primary element in the dependent handle pair, and valueRefIds will
     * contain the ObjectID for the secondary element (keyRefIds[i] keeps
     * valueRefIds[i] alive).
     *
     * NOTE: None of the objectIDs returned by ConditionalWeakTableElementReferences
     * are valid during the callback itself, as the GC may be in the middle
     * of moving objects from old to new. Thus profilers should not attempt
     * to inspect objects during a ConditionalWeakTableElementReferences call.
     * At GarbageCollectionFinished, all objects have been moved to their new
     * locations, and inspection may be done.
     */
    HResult ConditionalWeakTableElementReferences(
        uint cRootRefs,
        ObjectId* keyRefIds,
        ObjectId* valueRefIds,
        GCHandleId* rootIds);
}
