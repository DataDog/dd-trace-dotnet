namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerCallback4 : ICorProfilerCallback3
{
    public static new readonly Guid Guid = Guid.Parse("7B63B2E3-107D-4d48-B2F6-F61E229470D2");
    /*
     * Similar to JITCompilationStarted, except called when rejitting a method
     */
    HResult ReJITCompilationStarted(
                FunctionId functionId,
                ReJITId rejitId,
                int fIsSafeToBlock);

    /*
     * This is called exactly once per method (which may represent more than
     * one function id), to allow the code profiler to set alternate code
     * generation flags or a new method body.
     */
    HResult GetReJITParameters(
                ModuleId moduleId,
                MdMethodDef methodId,
                IntPtr pFunctionControl);

    /*
     * Similar to JITCompilationFinished, except called when rejitting a method
     */
    HResult ReJITCompilationFinished(
                FunctionId functionId,
                ReJITId rejitId,
                HResult hrStatus,
                int fIsSafeToBlock);

    /*
     * This is called to report an error encountered while processing a ReJIT request.
     * This may either be called from within the RequestReJIT call itself, or called after
     * RequestReJIT returns, if the error was encountered later on.
     */
    HResult ReJITError(
                ModuleId moduleId,
                MdMethodDef methodId,
                FunctionId functionId,
                HResult hrStatus);

    /*
     * The CLR calls MovedReferences with information about
     * object references that moved as a result of garbage collection.
     *
     * cMovedObjectIDRanges is a count of the number of ObjectID ranges that
     *      were moved.
     * oldObjectIDRangeStart is an array of elements, each of which is the start
     *      value of a range of ObjectID values before being moved.
     * newObjectIDRangeStart is an array of elements, each of which is the start
     *      value of a range of ObjectID values after being moved.
     * cObjectIDRangeLength is an array of elements, each of which states the
     *      size of the moved ObjectID value range.
     *
     * The last three arguments of this function are parallel arrays.
     *
     * In other words, if an ObjectID value lies within the range
     *      oldObjectIDRangeStart[i] <= ObjectID < oldObjectIDRangeStart[i] + cObjectIDRangeLength[i]
     * for 0 <= i < cMovedObjectIDRanges, then the ObjectID value has changed to
     *      ObjectID - oldObjectIDRangeStart[i] + newObjectIDRangeStart[i]
     *
     * NOTE: None of the objectIDs returned by MovedReferences are valid during the callback
     * itself, as the GC may be in the middle of moving objects from old to new. Thus profilers
     * should not attempt to inspect objects during a MovedReferences call. At
     * GarbageCollectionFinished, all objects have been moved to their new locations, and
     * inspection may be done.
     *
     * If the profiler implements ICorProfilerCallback4, ICorProfilerCallback4::MovedReferences2
     * is called first and ICorProfilerCallback::MovedReferences is called second but only if
     * ICorProfilerCallback4::MovedReferences2 returned success. Profilers can return failure
     * from ICorProfilerCallback4::MovedReferences2 to save some chattiness.
     */
    HResult MovedReferences2(
                uint cMovedObjectIDRanges,
                ObjectId* oldObjectIDRangeStart,
                ObjectId* newObjectIDRangeStart,
                nint* cObjectIDRangeLength);

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
     * If the profiler implements ICorProfilerCallback4, ICorProfilerCallback4::SurvivingReferences2
     * is called first and ICorProfilerCallback2::SurvivingReferences is called second but only if
     * ICorProfilerCallback4::SurvivingReferences2 returned success. Profilers can return failure
     * from ICorProfilerCallback4::SurvivingReferences2 to save some chattiness.
     */
    HResult SurvivingReferences2(
                uint cSurvivingObjectIDRanges,
                ObjectId* objectIDRangeStart,
                nint* cObjectIDRangeLength);
}
