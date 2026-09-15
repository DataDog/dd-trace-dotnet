// Private layout of ddog_prof_encoded_profile (opaque in the public header).
// Built by profile.c's ddog_prof_profile_serialize; read by encoded_profile.c
// (public accessors) and exporter.c (to build the upload request).
#pragma once

#include "datadog_poc/profiling.h"

struct ddog_prof_encoded_profile {
    uint8_t* data; // owned, already zstd-compressed pprof bytes
    size_t len;
    ddog_timespec start_time;
    ddog_timespec end_time;
};
