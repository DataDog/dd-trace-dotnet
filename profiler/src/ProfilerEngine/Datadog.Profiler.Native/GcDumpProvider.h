#pragma once

#include "IGcDumpProvider.h"

class GcDumpProvider : public IGcDumpProvider
{
public:
    GcDumpProvider();

public:
    gcdump_t Get() override;
};
