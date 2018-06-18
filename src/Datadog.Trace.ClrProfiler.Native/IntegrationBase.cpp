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

    std::vector<TypeReference> argumentTypes = instrumentedMethod.ArgumentTypes;

    const bool hasThis = instrumentedMethod.CorCallingConvention == IMAGE_CEE_CS_CALLCONV_HASTHIS ||
                         instrumentedMethod.CorCallingConvention == IMAGE_CEE_CS_CALLCONV_EXPLICITTHIS;

    if (hasThis)
    {
        // instance methods have an implicit first "this" parameter
        argumentTypes.insert(argumentTypes.begin(), instrumentedMethod.ContainingType);
    }

    const auto argumentCount = static_cast<INT32>(argumentTypes.size());
    pilr.CreateArray(GlobalTypeReferences.System_Object, argumentCount);

    // store each of the intrumented method's arguments into an object[],
    // if this is an instance method, the first argument will be "this"
    for (UINT16 i = 0; i < argumentCount; ++i)
    {
        const TypeReference& argumentType = argumentTypes[i];

        pilr.BeginLoadValueIntoArray(i);
        pilr.LoadArgument(i);

        if (NeedsBoxing(argumentType))
        {
            // values types need to be boxed before they are stored in an object[]
            pilr.Box(argumentType);
        }

        pilr.EndLoadValueIntoArray();
    }

    // note: the entry probe returns a Datadog.Trace.Scope instance,
    // which we will leave on the stack for the exit probe to pick up
    pilr.CallMember(entryProbe);
}

void IntegrationBase::InjectExitProbe(const ILRewriterWrapper& pilr,
                                      const MemberReference& instrumentedMethod,
                                      const MemberReference& exitProbe) const
{
    // if instrumented method's return value is a value type,
    // we need to box it before calling the exit probe
    if (NeedsBoxing(instrumentedMethod.ReturnType))
    {
        pilr.Box(instrumentedMethod.ReturnType);
    }

    // the entry probe returned a Datadog.Trace.Scope instance
    // and we left if on the stack, the exit probe will pop it
    // and leave a balanced stack
    pilr.CallMember(exitProbe);

    // if instrumented method's return value is a value type,
    // we need to unbox back into the stack before leaving
    if (NeedsBoxing(instrumentedMethod.ReturnType))
    {
        pilr.UnboxAny(instrumentedMethod.ReturnType);
    }
}

bool IntegrationBase::NeedsBoxing(const TypeReference& type)
{
    return type.CorElementType != ELEMENT_TYPE_VOID &&
           type.CorElementType != ELEMENT_TYPE_CLASS &&
           type.CorElementType != ELEMENT_TYPE_OBJECT &&
           type.CorElementType != ELEMENT_TYPE_STRING &&
           type.CorElementType != ELEMENT_TYPE_ARRAY &&
           type.CorElementType != ELEMENT_TYPE_SZARRAY;
}
