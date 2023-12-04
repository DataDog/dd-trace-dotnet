#pragma once

#include "IGcDumpProvider.h"
#include "GcDumpSession.h"

class GcDumpProvider : public IGcDumpProvider
{
public:
    GcDumpProvider();

public:
    gcdump_t Get() override;
    void Trigger() override;

private:
    GcDumpSession _session;
};
