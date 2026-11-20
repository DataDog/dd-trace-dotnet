#include "datadog_poc/profiling.h"
#include "internal.h"

// Deliberately doesn't reject an empty base_url/site/api_key here, matching
// real libdatadog: construction is tolerant of degenerate input (the
// wrapper's own asserts are the guard rail in debug builds - see
// ExporterBuilder::CreateEndpoint - and NDEBUG builds are expected to
// construct successfully and fail later, at actual send time, instead).
ddog_error_code ddog_prof_endpoint_agent(ddog_charslice base_url, uint64_t timeout_ms, bool use_system_resolver,
                                          ddog_prof_endpoint* out_endpoint)
{
    if (out_endpoint == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "out_endpoint is NULL");
    }

    out_endpoint->kind = DDOG_POC_PROF_ENDPOINT_AGENT;
    out_endpoint->url_or_site = base_url;
    out_endpoint->api_key.ptr = NULL;
    out_endpoint->api_key.len = 0;
    out_endpoint->timeout_ms = timeout_ms;
    out_endpoint->use_system_resolver = use_system_resolver;
    return DDOG_OK;
}

ddog_error_code ddog_prof_endpoint_agentless(ddog_charslice site, ddog_charslice api_key, uint64_t timeout_ms,
                                              bool use_system_resolver, ddog_prof_endpoint* out_endpoint)
{
    if (out_endpoint == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "out_endpoint is NULL");
    }

    out_endpoint->kind = DDOG_POC_PROF_ENDPOINT_AGENTLESS;
    out_endpoint->url_or_site = site;
    out_endpoint->api_key = api_key;
    out_endpoint->timeout_ms = timeout_ms;
    out_endpoint->use_system_resolver = use_system_resolver;
    return DDOG_OK;
}
