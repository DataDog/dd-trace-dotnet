// Private layout of ddog_prof_encoded_profile (opaque in the public header).
// Built by profile.c's ddog_prof_profile_serialize; read by encoded_profile.c
// (public accessors) and exporter.c (to build the upload request).
#pragma once

#include "datadog_poc/profiling.h"
#include "profile.h"

struct ddog_prof_encoded_profile {
    uint8_t* data; // owned, already zstd-compressed pprof bytes
    size_t len;
    ddog_timespec start_time;
    ddog_timespec end_time;
    // Moved out of the profile at serialize time (libdatadog's
    // EncodedProfile.endpoints_stats). One entry per distinct endpoint.
    ddog_prof_endpoint_count_owned* endpoint_counts; // owned
    size_t endpoint_counts_len;
};
