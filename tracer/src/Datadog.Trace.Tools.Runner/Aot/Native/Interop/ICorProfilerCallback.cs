// <copyright file="ICorProfilerCallback.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerCallback : IUnknown
{
    public static new readonly Guid Guid = Guid.Parse("8a8cc829-ccf2-49fe-bbae-0f022228071a");

    HResult Initialize(IntPtr pICorProfilerInfoUnk)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.Initialize");
        return HResult.E_NOTIMPL;
    }

    HResult Shutdown()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.Shutdown");
        return HResult.E_NOTIMPL;
    }

    HResult AppDomainCreationStarted(AppDomainId appDomainId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.AppDomainCreationStarted");
        return HResult.E_NOTIMPL;
    }
    HResult AppDomainCreationFinished(AppDomainId appDomainId, HResult hrStatus)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.AppDomainCreationFinished");
        return HResult.E_NOTIMPL;
    }

    HResult AppDomainShutdownStarted(AppDomainId appDomainId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.AppDomainShutdownStarted");
        return HResult.E_NOTIMPL;
    }
    HResult AppDomainShutdownFinished(AppDomainId appDomainId, HResult hrStatus)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.AppDomainShutdownFinished");
        return HResult.E_NOTIMPL;
    }

    HResult AssemblyLoadStarted(AssemblyId assemblyId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.AssemblyLoadStarted");
        return HResult.E_NOTIMPL;
    }
    HResult AssemblyLoadFinished(AssemblyId assemblyId, HResult hrStatus)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.AssemblyLoadFinished");
        return HResult.E_NOTIMPL;
    }

    HResult AssemblyUnloadStarted(AssemblyId assemblyId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.AssemblyUnloadStarted");
        return HResult.E_NOTIMPL;
    }
    HResult AssemblyUnloadFinished(AssemblyId assemblyId, HResult hrStatus)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.AssemblyUnloadFinished");
        return HResult.E_NOTIMPL;
    }

    HResult ModuleLoadStarted(ModuleId moduleId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ModuleLoadStarted");
        return HResult.E_NOTIMPL;
    }
    HResult ModuleLoadFinished(ModuleId moduleId, HResult hrStatus)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ModuleLoadFinished");
        return HResult.E_NOTIMPL;
    }

    HResult ModuleUnloadStarted(ModuleId moduleId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ModuleUnloadStarted");
        return HResult.E_NOTIMPL;
    }
    HResult ModuleUnloadFinished(ModuleId moduleId, HResult hrStatus)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ModuleUnloadFinished");
        return HResult.E_NOTIMPL;
    }

    HResult ModuleAttachedToAssembly(ModuleId moduleId, AssemblyId assemblyId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ModuleAttachedToAssembly");
        return HResult.E_NOTIMPL;
    }

    HResult ClassLoadStarted(ClassId classId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ClassLoadStarted");
        return HResult.E_NOTIMPL;
    }
    HResult ClassLoadFinished(ClassId classId, HResult hrStatus)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ClassLoadFinished");
        return HResult.E_NOTIMPL;
    }

    HResult ClassUnloadStarted(ClassId classId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ClassUnloadStarted");
        return HResult.E_NOTIMPL;
    }
    HResult ClassUnloadFinished(ClassId classId, HResult hrStatus)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ClassUnloadFinished");
        return HResult.E_NOTIMPL;
    }

    HResult FunctionUnloadStarted(FunctionId functionId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.FunctionUnloadStarted");
        return HResult.E_NOTIMPL;
    }

    HResult JITCompilationStarted(FunctionId functionId, int fIsSafeToBlock)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.JITCompilationStarted");
        return HResult.E_NOTIMPL;
    }
    HResult JITCompilationFinished(FunctionId functionId, HResult hrStatus, int fIsSafeToBlock)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.JITCompilationFinished");
        return HResult.E_NOTIMPL;
    }

    HResult JITCachedFunctionSearchStarted(FunctionId functionId, out int pbUseCachedFunction)
    {
        pbUseCachedFunction = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.JITCachedFunctionSearchStarted");
        return HResult.E_NOTIMPL;
    }
    HResult JITCachedFunctionSearchFinished(FunctionId functionId, COR_PRF_JIT_CACHE result)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.JITCachedFunctionSearchFinished");
        return HResult.E_NOTIMPL;
    }

    HResult JITFunctionPitched(FunctionId functionId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.JITFunctionPitched");
        return HResult.E_NOTIMPL;
    }

    HResult JITInlining(FunctionId callerId, FunctionId calleeId, out int pfShouldInline)
    {
        pfShouldInline = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.JITInlining");
        return HResult.E_NOTIMPL;
    }

    HResult ThreadCreated(ThreadId threadId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ThreadCreated");
        return HResult.E_NOTIMPL;
    }
    HResult ThreadDestroyed(ThreadId threadId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ThreadDestroyed");
        return HResult.E_NOTIMPL;
    }
    HResult ThreadAssignedToOSThread(ThreadId managedThreadId, int osThreadId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ThreadAssignedToOSThread");
        return HResult.E_NOTIMPL;
    }

    HResult RemotingClientInvocationStarted()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RemotingClientInvocationStarted");
        return HResult.E_NOTIMPL;
    }
    HResult RemotingClientSendingMessage(in Guid pCookie, int fIsAsync)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RemotingClientSendingMessage");
        return HResult.E_NOTIMPL;
    }
    HResult RemotingClientReceivingReply(in Guid pCookie, int fIsAsync)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RemotingClientReceivingReply");
        return HResult.E_NOTIMPL;
    }
    HResult RemotingClientInvocationFinished()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RemotingClientInvocationFinished");
        return HResult.E_NOTIMPL;
    }

    HResult RemotingServerReceivingMessage(in Guid pCookie, int fIsAsync)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RemotingServerReceivingMessage");
        return HResult.E_NOTIMPL;
    }
    HResult RemotingServerInvocationStarted()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RemotingServerInvocationStarted");
        return HResult.E_NOTIMPL;
    }
    HResult RemotingServerInvocationReturned()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RemotingServerInvocationReturned");
        return HResult.E_NOTIMPL;
    }
    HResult RemotingServerSendingReply(in Guid pCookie, int fIsAsync)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RemotingServerSendingReply");
        return HResult.E_NOTIMPL;
    }

    HResult UnmanagedToManagedTransition(FunctionId functionId, COR_PRF_TRANSITION_REASON reason)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.UnmanagedToManagedTransition");
        return HResult.E_NOTIMPL;
    }
    HResult ManagedToUnmanagedTransition(FunctionId functionId, COR_PRF_TRANSITION_REASON reason)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ManagedToUnmanagedTransition");
        return HResult.E_NOTIMPL;
    }

    HResult RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RuntimeSuspendStarted");
        return HResult.E_NOTIMPL;
    }
    HResult RuntimeSuspendFinished()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RuntimeSuspendFinished");
        return HResult.E_NOTIMPL;
    }
    HResult RuntimeSuspendAborted()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RuntimeSuspendAborted");
        return HResult.E_NOTIMPL;
    }

    HResult RuntimeResumeStarted()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RuntimeResumeStarted");
        return HResult.E_NOTIMPL;
    }
    HResult RuntimeResumeFinished()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RuntimeResumeFinished");
        return HResult.E_NOTIMPL;
    }

    HResult RuntimeThreadSuspended(ThreadId threadId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RuntimeThreadSuspended");
        return HResult.E_NOTIMPL;
    }
    HResult RuntimeThreadResumed(ThreadId threadId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RuntimeThreadResumed");
        return HResult.E_NOTIMPL;
    }

    HResult MovedReferences(
        uint cMovedObjectIDRanges,
        ObjectId* oldObjectIDRangeStart,
        ObjectId* newObjectIDRangeStart,
        uint* cObjectIDRangeLength)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.MovedReferences");
        return HResult.E_NOTIMPL;
    }

    HResult ObjectAllocated(ObjectId objectId, ClassId classId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ObjectAllocated");
        return HResult.E_NOTIMPL;
    }

    HResult ObjectsAllocatedByClass(uint cClassCount, ClassId* classIds, uint* cObjects)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ObjectsAllocatedByClass");
        return HResult.E_NOTIMPL;
    }

    HResult ObjectReferences(
        ObjectId objectId,
        ClassId classId,
        uint cObjectRefs,
        ObjectId* objectRefIds)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ObjectReferences");
        return HResult.E_NOTIMPL;
    }

    HResult RootReferences(uint cRootRefs, ObjectId* rootRefIds)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.RootReferences");
        return HResult.E_NOTIMPL;
    }

    HResult ExceptionThrown(ObjectId thrownObjectId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionThrown");
        return HResult.E_NOTIMPL;
    }

    HResult ExceptionSearchFunctionEnter(FunctionId functionId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionSearchFunctionEnter");
        return HResult.E_NOTIMPL;
    }
    HResult ExceptionSearchFunctionLeave()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionSearchFunctionLeave");
        return HResult.E_NOTIMPL;
    }

    HResult ExceptionSearchFilterEnter(FunctionId functionId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionSearchFilterEnter");
        return HResult.E_NOTIMPL;
    }
    HResult ExceptionSearchFilterLeave()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionSearchFilterLeave");
        return HResult.E_NOTIMPL;
    }

    HResult ExceptionSearchCatcherFound(FunctionId functionId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionSearchCatcherFound");
        return HResult.E_NOTIMPL;
    }

    HResult ExceptionOSHandlerEnter(nint* __unused)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionOSHandlerEnter");
        return HResult.E_NOTIMPL;
    }
    HResult ExceptionOSHandlerLeave(nint* __unused)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionOSHandlerLeave");
        return HResult.E_NOTIMPL;
    }

    HResult ExceptionUnwindFunctionEnter(FunctionId functionId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionUnwindFunctionEnter");
        return HResult.E_NOTIMPL;
    }
    HResult ExceptionUnwindFunctionLeave()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionUnwindFunctionLeave");
        return HResult.E_NOTIMPL;
    }

    HResult ExceptionUnwindFinallyEnter(FunctionId functionId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionUnwindFinallyEnter");
        return HResult.E_NOTIMPL;
    }
    HResult ExceptionUnwindFinallyLeave()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionUnwindFinallyLeave");
        return HResult.E_NOTIMPL;
    }

    HResult ExceptionCatcherEnter(FunctionId functionId, ObjectId objectId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionCatcherEnter");
        return HResult.E_NOTIMPL;
    }
    HResult ExceptionCatcherLeave()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionCatcherLeave");
        return HResult.E_NOTIMPL;
    }

    HResult COMClassicVTableCreated(
        ClassId wrappedClassId,
        in Guid implementedIID,
        void* pVTable,
        uint cSlots)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.COMClassicVTableCreated");
        return HResult.E_NOTIMPL;
    }

    HResult COMClassicVTableDestroyed(
        ClassId wrappedClassId,
        in Guid implementedIID,
        void* pVTable)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.COMClassicVTableDestroyed");
        return HResult.E_NOTIMPL;
    }

    HResult ExceptionCLRCatcherFound()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionCLRCatcherFound");
        return HResult.E_NOTIMPL;
    }
    HResult ExceptionCLRCatcherExecute()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback.ExceptionCLRCatcherExecute");
        return HResult.E_NOTIMPL;
    }
}
#endif
