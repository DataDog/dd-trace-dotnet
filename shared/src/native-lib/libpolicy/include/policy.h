#pragma once

#include <policy_reader.h>

#include <dd_types.h>

/**
 * @brief Retrieves a vector of policies from the provided buffer.
 *
 * @param buffer Pointer to the buffer containing a FlatBuffers Policies root
 * object.
 * @return A vector of policies or NULL if the buffer is invalid.
 */
dd_ns(Policy_vec_t) get_policies(uint8_t *buffer);
