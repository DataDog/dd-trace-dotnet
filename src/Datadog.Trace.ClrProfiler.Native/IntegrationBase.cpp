#include "IntegrationBase.h"
#include "GlobalTypeReferences.h"

void IntegrationBase::InjectEntryProbe(const ILRewriterWrapper& pilr,
                                       const ModuleID moduleID,
                                       const mdMethodDef methodDef,
                                       const MemberReference& entryProbe) const
{
    const IntegrationType integrationType = GetIntegrationType();

    pilr.LoadInt32(integrationType);
    pilr.LoadInt64(moduleID);
    pilr.LoadInt32(methodDef);

    // allow inheritors to create an object[] with additional arguments
    InjectEntryArguments(pilr);

    // note: the entry probe returns a Datadog.Trace.Scope instance,
    // which we will leave on the stack for the exit probe to pick up
    pilr.CallMember(entryProbe);
}

void IntegrationBase::InjectEntryArguments(const ILRewriterWrapper& pilr) const
{
    pilr.CreateArray(GlobalTypeReferences.System_Object, 0);
}

void IntegrationBase::InjectExitProbe(const ILRewriterWrapper& pilr,
                                      const MemberReference& exitProbe) const
{
    // note: the entry probe returned a Datadog.Trace.Scope instance
    // and we left if on the stack, the exit probe will pop it
    // and leave a balanced stack
    pilr.CallMember(exitProbe);
}
