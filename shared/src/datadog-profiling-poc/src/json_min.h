// Just enough JSON to build the multipart upload's "event" part. The shape
// of that object is fixed and small (see plan doc "Exporter (libcurl)"), so
// exporter.c assembles it by hand with literal C strings for keys/punctuation.
// This file provides the primitives: an escaped-string writer, a validator
// for the caller-provided internal/info JSON documents (which are copied
// verbatim into the event), and the event tail shared by both exporters.
#pragma once

#include "internal.h"
#include "profile.h"

// Appends `value`, JSON-escaped and double-quoted, to `buf`.
// Returns false on allocation failure.
bool ddog__json_append_escaped_string(ddog__buf* buf, ddog_charslice value);

// Returns true if `value` is exactly one well-formed JSON value (RFC 8259),
// optionally surrounded by whitespace.
bool ddog__json_is_valid(ddog_charslice value);

// Appends the end of the event object, like libdatadog's build_event_json:
//   ,"endpoint_counts":{...}|null,"process_tags":"..."|null,"internal":{...},"info":{...}}
// `internal_json`/`info_json` must have been checked with ddog__json_is_valid;
// NULL means "not provided" and is written as {}.
bool ddog__json_append_event_tail(ddog__buf* buf, const ddog_prof_endpoint_count_owned* endpoint_counts,
                                  size_t endpoint_counts_len, const ddog_charslice* process_tags,
                                  const ddog_charslice* internal_json, const ddog_charslice* info_json);

// Appends the event's "attachments" array: the additional files' names, then
// `profile_name` (libdatadog lists the additional files first and the
// profile last).
bool ddog__json_append_attachments(ddog__buf* buf, const ddog_prof_exporter_file* files_to_compress,
                                   size_t files_to_compress_len, const char* profile_name);
