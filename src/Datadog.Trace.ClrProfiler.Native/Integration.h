#pragma once

#include <vector>
#include <corhlpr.h>
#include <corprof.h>
#include "IntegrationType.h"

// forwards declarations
class ILRewriterWrapper;

class Integration
{
public:
    Integration(const bool is_enabled,
                    const IntegrationType integration,
                    std::vector<MemberReference> member_references)
        : m_isEnabled(is_enabled),
          m_integrationType(integration),
          m_InstrumentedMethods(std::move(member_references))
    {
    }

    bool IsEnabled() const
    {
        return m_isEnabled;
    }

    IntegrationType GetIntegrationType() const
    {
        return m_integrationType;
    }

    const std::vector<MemberReference>& GetInstrumentedMethods() const
    {
        return m_InstrumentedMethods;
    }

    void InjectEntryProbe(const ILRewriterWrapper& pilr,
                          ModuleID moduleID,
                          mdMethodDef methodDef,
                          const MemberReference& instrumentedMethod,
                          const MemberReference& entryProbe) const;

    void InjectExitProbe(const ILRewriterWrapper& pilr,
                         const MemberReference& instrumentedMethod,
                         const MemberReference& exitProbe) const;

private :
    bool m_isEnabled;
    IntegrationType m_integrationType;
    std::vector<MemberReference> m_InstrumentedMethods;

    static bool NeedsBoxing(const TypeReference& type);
};
