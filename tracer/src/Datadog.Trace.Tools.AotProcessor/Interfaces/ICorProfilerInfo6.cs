namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerInfo6 : ICorProfilerInfo5
{
    public static new readonly Guid Guid = new("F30A070D-BFFB-46A7-B1D8-8781EF7B698A");

    /*
    * Returns an enumerator for all methods that
    * - belong to a given NGen or R2R module (inlinersModuleId) and
    * - inlined a body of a given method (inlineeModuleId / inlineeMethodId).
    *
    * If incompleteData is set to TRUE after function is called, it means that the methods enumerator
    * doesn't contain all methods inlining a given method.
    * It can happen when one or more direct or indirect dependencies of inliners module haven't been loaded yet.
    * If profiler needs accurate data it should retry later when more modules are loaded (preferably on each module load).
    *
    * It can be used to lift limitation on inlining for ReJIT.
    *
    * NOTE: If the inlinee method is decorated with the System.Runtime.Versioning.NonVersionable attribute then
    * then some inliners may not ever be reported. If you need to get a full accounting you can avoid the issue
    * by disabling the use of all native images.
    *
    */
    HResult EnumNgenModuleMethodsInliningThisMethod(
        ModuleId inlinersModuleId,
        ModuleId inlineeModuleId,
        MdMethodDef inlineeMethodId,
        out int incompleteData,
        out IntPtr ppEnum);
}
