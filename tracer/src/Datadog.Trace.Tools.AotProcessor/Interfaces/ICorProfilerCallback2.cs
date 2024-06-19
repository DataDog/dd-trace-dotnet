namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerCallback2 : ICorProfilerCallback
{
    public static new readonly Guid Guid = Guid.Parse("8A8CC829-CCF2-49fe-BBAE-0F022228071A");

    /*
     *
     * THREAD EVENTS
     *
     */

    /*
     * The CLR calls ThreadNameChanged to notify the code profiler
     * that a thread's name has changed.
     *
     * name is not NULL terminated.
     *
     */
    HResult ThreadNameChanged(
                ThreadId threadId,
                uint cchName,
                char* name);

    /*
     *
     * GARBAGE COLLECTION EVENTS
     *
     */

    /*
     * The CLR calls GarbageCollectionStarted before beginning a
     * garbage collection. All GC callbacks pertaining to this
     * collection will occur between the GarbageCollectionStarted
     * callback and the corresponding GarbageCollectionFinished
     * callback. Corresponding GarbageCollectionStarted and
     * GarbageCollectionFinished callbacks need not occur on the same thread.
     *
     *          cGenerations indicates the total number of entries in
     *                the generationCollected array
     *          generationCollected is an array of booleans, indexed
     *                by COR_PRF_GC_GENERATIONS, indicating which
     *                generations are being collected in this collection
     *          reason indicates whether this GC was induced
     *                by the application calling GC.Collect().
     *
     * NOTE: It is safe to inspect objects in their original locations
     * during this callback. The GC will begin moving objects after
     * the profiler returns from this callback. Therefore, after
     * returning, the profiler should consider all ObjectIDs to be invalid
     * until it receives a GarbageCollectionFinished callback.
     */
    HResult GarbageCollectionStarted(
                int cGenerations,
                int* generationCollected,
                COR_PRF_GC_REASON reason);

    /*
     * The CLR calls SurvivingReferences with information about
     * object references that survived a garbage collection.
     *
     * Generally, the CLR calls SurvivingReferences for non-compacting garbage collections.
     * For compacting garbage collections, MovedReferences is called instead.
     *
     * The exception to this rule is that the CLR always calls SurvivingReferences for objects
     * in the large object heap, which is not compacted.
     *
     * Multiple calls to SurvivingReferences may be received during a particular
     * garbage collection, due to limited internal buffering, multiple threads reporting
     * in the case of server gc, and other reasons.
     * In the case of multiple calls, the information is cumulative - all of the references
     * reported in any SurvivingReferences call survive this collection.
     *
     * cSurvivingObjectIDRanges is a count of the number of ObjectID ranges that
     *      survived.
     * objectIDRangeStart is an array of elements, each of which is the start
     *      value of a range of ObjectID values that survived the collection.
     * cObjectIDRangeLength is an array of elements, each of which states the
     *      size of the surviving ObjectID value range.
     *
     * The last two arguments of this function are parallel arrays.
     *
     * In other words, if an ObjectID value lies within the range
     *      objectIDRangeStart[i] <= ObjectID < objectIDRangeStart[i] + cObjectIDRangeLength[i]
     * for 0 <= i < cMovedObjectIDRanges, then the ObjectID has survived the collection
     *
     * THIS CALLBACK IS OBSOLETE. It reports ranges for objects >4GB as UINT32_MAX
     * on 64-bit platforms. Use ICorProfilerCallback4::SurvivingReferences2 instead.
     */
    HResult SurvivingReferences(
                uint cSurvivingObjectIDRanges,
                ObjectId* objectIDRangeStart,
                uint* cObjectIDRangeLength);
    /*
     * The CLR calls GarbageCollectionFinished after a garbage
     * collection has completed and all GC callbacks have been
     * issued for it.
     *
     * NOTE: It is now safe to inspect objects in their
     * final locations.
     */
    HResult GarbageCollectionFinished();

    /*
     * The CLR calls FinalizeableObjectQueued to notify the code profiler
     * that an object with a finalizer (destructor in C# parlance) has
     * just been queued to the finalizer thread for execution of its
     * Finalize method.
     *
     * finalizerFlags describes aspects of the finalizer, and takes its
     *     value from COR_PRF_FINALIZER_FLAGS.
     *
     */

    HResult FinalizeableObjectQueued(
        COR_PRF_FINALIZER_FLAGS finalizerFlags,
        ObjectId objectID);

    /*
     * The CLR calls RootReferences2 with information about root
     * references after a garbage collection has occurred.
     * For each root reference in rootRefIds, there is information in
     * rootClassifications to classify it. Depending on the classification,
     * rootsIds may contain additional information. The information in
     * rootKinds and rootFlags contains information about the location and
     * properties of the reference.
     *
     * If the profiler implements ICorProfilerCallback2, both
     * ICorProfilerCallback::RootReferences and ICorProfilerCallback2::RootReferences2
     * are called. As the information passed to RootReferences2 is a superset
     * of the one passed to RootReferences, profilers will normally implement
     * one or the other, but not both.
     *
     * If the root kind is STACK, the ID is the FunctionID of the
     * function containing the variable. If the FunctionID is 0, the function
     * is an unnamed function internal to the CLR.
     *
     * If the root kind is HANDLE, the ID is the GCHandleID.
     *
     * For the other root kinds, the ID is an opaque value and should
     * be ignored.
     *
     * It's possible for entries in rootRefIds to be 0 - this just
     * implies the corresponding root reference was null and thus did not
     * refer to an object on the managed heap.
     *
     * NOTE: None of the objectIDs returned by RootReferences2 are valid during the callback
     * itself, as the GC may be in the middle of moving objects from old to new. Thus profilers
     * should not attempt to inspect objects during a RootReferences2 call. At
     * GarbageCollectionFinished, all objects have been moved to their new locations, and
     * inspection may be done.
     */

    HResult RootReferences2(
                uint cRootRefs,
                ObjectId* rootRefIds,
                COR_PRF_GC_ROOT_KIND* rootKinds,
                COR_PRF_GC_ROOT_FLAGS* rootFlags,
                uint* rootIds);

    /*
     * The CLR calls HandleCreated when a gc handle has been created.
     *
     */

    HResult HandleCreated(
                GCHandleId handleId,
                ObjectId initialObjectId);

    /*
     * The CLR calls HandleDestroyed when a gc handle has been destroyed.
     *
     */

    HResult HandleDestroyed(
                GCHandleId handleId);
}
