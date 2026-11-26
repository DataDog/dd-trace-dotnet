#include "datadog_poc/common.h"
#include "internal.h"
#include "tags.h"

#include <stdlib.h>

void ddog_vec_tag_new(ddog_vec_tag* out_tags)
{
    out_tags->ptr = NULL;
    out_tags->len = 0;
    out_tags->capacity = 0;
}

ddog_error_code ddog_vec_tag_push(ddog_vec_tag* tags, ddog_charslice key, ddog_charslice value)
{
    if (tags == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "tags is NULL");
    }
    if (key.len == 0 || key.ptr == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "tag key must not be empty");
    }

    if (tags->len == tags->capacity)
    {
        size_t new_capacity = tags->capacity == 0 ? 8 : tags->capacity * 2;
        ddog_tag* new_ptr = (ddog_tag*)realloc(tags->ptr, new_capacity * sizeof(ddog_tag));
        if (new_ptr == NULL)
        {
            return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to grow tag vector to %zu entries", new_capacity);
        }
        tags->ptr = new_ptr;
        tags->capacity = new_capacity;
    }

    ddog_tag* slot = &tags->ptr[tags->len];
    if (!ddog__charslice_dup(key, &slot->key))
    {
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy tag key");
    }
    if (!ddog__charslice_dup(value, &slot->value))
    {
        ddog__charslice_free(&slot->key);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy tag value");
    }

    tags->len += 1;
    return DDOG_OK;
}

void ddog_vec_tag_drop(ddog_vec_tag* tags)
{
    if (tags == NULL)
    {
        return;
    }

    for (size_t i = 0; i < tags->len; i++)
    {
        ddog__charslice_free(&tags->ptr[i].key);
        ddog__charslice_free(&tags->ptr[i].value);
    }
    free(tags->ptr);
    tags->ptr = NULL;
    tags->len = 0;
    tags->capacity = 0;
}
