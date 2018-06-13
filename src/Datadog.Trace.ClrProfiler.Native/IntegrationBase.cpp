#include "IntegrationBase.h"
#include "GlobalTypeReferences.h"

void IntegrationBase::InjectEntryProbe(const ILRewriterWrapper& pilr,
                                       const ModuleID moduleID,
                                       const mdMethodDef methodDef,
                                       const MemberReference& instrumentedMethod,
                                       const MemberReference& entryProbe) const
{
    const IntegrationType integrationType = GetIntegrationType();

    pilr.LoadInt32(integrationType);
    pilr.LoadInt64(moduleID);
    pilr.LoadInt32(methodDef);

    // allow inheritors to create an object[] with additional arguments
    InjectEntryArguments(pilr, instrumentedMethod);

    // note: the entry probe returns a Datadog.Trace.Scope instance,
    // which we will leave on the stack for the exit probe to pick up
    pilr.CallMember(entryProbe);
}

void IntegrationBase::InjectEntryArguments(const ILRewriterWrapper& pilr,
                                           const MemberReference& instrumentedMethod) const
{
    // instance methods has an implicit first "this" parameter
    const bool hasThis = instrumentedMethod.CorCallingConvention == IMAGE_CEE_CS_CALLCONV_HASTHIS ||
                         instrumentedMethod.CorCallingConvention == IMAGE_CEE_CS_CALLCONV_EXPLICITTHIS;

    const auto argumentCount = static_cast<INT32>(instrumentedMethod.ArgumentTypes.size());
    pilr.CreateArray(GlobalTypeReferences.System_Object, argumentCount);

    for (UINT16 i = 0; i < argumentCount; ++i)
    {
        const TypeReference& argumentType = instrumentedMethod.ArgumentTypes[i];

        pilr.BeginLoadValueIntoArray(i);
        pilr.LoadArgument(hasThis ? i + 1 : i);

        if (argumentType.CorElementType != ELEMENT_TYPE_CLASS &&
            argumentType.CorElementType != ELEMENT_TYPE_OBJECT &&
            argumentType.CorElementType != ELEMENT_TYPE_STRING &&
            argumentType.CorElementType != ELEMENT_TYPE_ARRAY &&
            argumentType.CorElementType != ELEMENT_TYPE_SZARRAY)
        {
            // values types need to be boxed before they are stored in an object[]
            pilr.Box(argumentType);
        }

        pilr.EndLoadValueIntoArray();
    }
}

void IntegrationBase::InjectExitProbe(const ILRewriterWrapper& pilr,
                                      const MemberReference& exitProbe) const
{
    // note: the entry probe returned a Datadog.Trace.Scope instance
    // and we left if on the stack, the exit probe will pop it
    // and leave a balanced stack
    pilr.CallMember(exitProbe);
}
