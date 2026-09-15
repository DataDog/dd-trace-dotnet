#include "datadog_poc/profiling.h"
#include "encoded_profile.h"
#include "internal.h"

#include <stdlib.h>

ddog_error_code ddog_prof_encoded_profile_bytes(ddog_prof_encoded_profile* encoded, const uint8_t** out_ptr, size_t* out_len)
{
    struct ddog_prof_encoded_profile* impl = (struct ddog_prof_encoded_profile*)encoded;
    if (impl == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "encoded is NULL");
    }
    if (out_ptr == NULL || out_len == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "out_ptr/out_len is NULL");
    }

    *out_ptr = impl->data;
    *out_len = impl->len;
    return DDOG_OK;
}

void ddog_prof_encoded_profile_drop(ddog_prof_encoded_profile* encoded)
{
    struct ddog_prof_encoded_profile* impl = (struct ddog_prof_encoded_profile*)encoded;
    if (impl == NULL)
    {
        return;
    }
    free(impl->data);
    free(impl);
}
