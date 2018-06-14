#pragma once

#include <vector>
#include <corhlpr.h>
#include <corprof.h>
#include "TypeReference.h"
#include "MemberReference.h"
#include "IntegrationType.h"

// forwards declarations
class ILRewriterWrapper;

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

    void InjectEntryProbe(const ILRewriterWrapper& pilr,
                          ModuleID moduleID,
                          mdMethodDef methodDef,
                          const MemberReference& instrumentedMethod,
                          const MemberReference& entryProbe) const;

    void InjectExitProbe(const ILRewriterWrapper& pilr,
                         const MemberReference& instrumentedMethod,
                         const MemberReference& exitProbe) const;

protected:
    virtual ~IntegrationBase() = default;
    std::vector<MemberReference> m_InstrumentedMethods = {};

private :
    static bool NeedsBoxing(const TypeReference& type);
};
