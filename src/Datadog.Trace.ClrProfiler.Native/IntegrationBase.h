#pragma once

#include <vector>
#include <corhlpr.h>
#include "TypeReference.h"
#include "MemberReference.h"
#include "IntegrationType.h"
#include "ILRewriterWrapper.h"

class IntegrationBase
{
public:
    IntegrationBase() = default;

    virtual bool IsEnabled() const = 0;

    virtual IntegrationType GetIntegrationType() const = 0;

    const std::vector<MemberReference>& GetInstrumentedMethods() const
    {
        return m_InstrumentedMethods;
    }

    const std::vector<TypeReference>& GetTypeReferences() const
    {
        return m_TypeReferences;
    }

    const std::vector<MemberReference>& GetMemberReferences() const
    {
        return m_MemberReferences;
    }

    void InjectEntryProbe(const ILRewriterWrapper& pilr,
                          ModuleID moduleID,
                          mdMethodDef methodDef,
                          const MemberReference& instrumentedMethod,
                          const MemberReference& entryProbe) const;

    void InjectExitProbe(const ILRewriterWrapper& pilr,
                         const MemberReference& exitProbe) const;

protected:
    virtual ~IntegrationBase() = default;
    std::vector<MemberReference> m_InstrumentedMethods = {};
    std::vector<TypeReference> m_TypeReferences = {};
    std::vector<MemberReference> m_MemberReferences = {};

    virtual void InjectEntryArguments(const ILRewriterWrapper& pilr, const MemberReference& instrumentedMethod) const;
};
