// Private layout of the opaque ddog_tag element type declared in
// datadog_poc/common.h. Visible to exporter.c so it can read tag contents when
// building the outgoing request; never exposed to callers of the library.
#pragma once

#include "datadog_poc/common.h"

struct ddog_tag {
    ddog_charslice key;   // owned copy
    ddog_charslice value; // owned copy
};
