// Hand-rolled pprof (Google's standard protobuf profile schema) encoder.
// Deliberately minimal - see plan doc "pprof encoding": only a varint + tag
// writer, no general protobuf library, no dedup of Mapping/Function/Location
// (only string interning is mandatory and is done here).
#pragma once

#include "internal.h"
#include "profile.h"

// Appends the raw (uncompressed) pprof-encoded bytes for the profile's
// currently-accumulated samples to `out`. start_time/end_time become the
// encoded Profile's time_nanos/duration_nanos fields. Returns false on
// allocation failure.
bool ddog__pprof_encode(const struct ddog_prof_profile* profile, const ddog_timespec* start_time,
                        const ddog_timespec* end_time, ddog__buf* out);
