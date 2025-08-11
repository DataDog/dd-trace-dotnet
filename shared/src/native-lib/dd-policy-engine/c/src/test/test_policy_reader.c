/*
 * Unit test stubs for policy reader behavior (utest.h)
 *
 * These tests validate that:
 *  - get_policies(NULL) returns NULL
 *  - get_policies() returns NULL for invalid/non-flatbuffer data
 *  - (optionally) with a generated FlatBuffers header, get_policies()
 *    returns a non-NULL vector with a positive length
 *
 * Build:
 *  - Ensure the FlatBuffers C readers are generated (make -C c all)
 *  - Tests are compiled together with test.c which provides UTEST_MAIN()
 *
 * Optional integration:
 *  - Run the Go example to generate a header with an embedded policy buffer:
 *      make -C ../go example_generate_c_header_buffer
 *    or
 *      make -C ../go example_json_to_hardcoded_injector_policies
 *  - This test will pick up the header (if present) and perform an integration read.
 */
#define _GNU_SOURCE
#include "hardcoded_policies.h"
#include "utest/utest.h"

#include <stddef.h>
#include <stdint.h>
#include <string.h>

#include "policy.h" /* get_policies(...) */
#include "policy_builder.h"
#include "policy_verifier.h"
#include "wire/dd_types.h" /* dd_ns(...) */

UTEST(policy_reader, null_buffer_returns_null_vector) {
  dd_ns(Policy_vec_t) v = plcs_get_policies(NULL, 0);
  ASSERT_TRUE(v == NULL);
}

UTEST(policy_reader, invalid_buffer_returns_null_vector) {
  /* A tiny, invalid buffer that should not parse as a FlatBuffers root. */
  static const uint8_t bogus[] = {0x00, 0x01, 0x02, 0x03};
  dd_ns(Policy_vec_t) v = plcs_get_policies(bogus, sizeof(bogus));
  ASSERT_TRUE(v == NULL);
}

UTEST(policy_reader_integration, valid_buffer_vector_has_elements_if_header_available) {
  dd_ns(Policy_vec_t) v = plcs_get_policies(hardcoded_policies, hardcoded_policies_len);
  ASSERT_TRUE(v != NULL);

  size_t len = dd_ns(Policy_vec_len)(v);
  ASSERT_TRUE(len > 0);
}

UTEST(policy_reader, null_policices) {
  flatcc_builder_t b;
  size_t sz;
  flatcc_builder_init(&b);

  dd_ns(Policies_start_as_root(&b));
  /* Do NOT set Policies_policies(...) */
  dd_ns(Policies_end_as_root(&b));

  void *buf = flatcc_builder_finalize_buffer(&b, &sz);

  /* This should pass verify, since the field is optional. */
  ASSERT_TRUE(dd_ns(Policies_verify_as_root(buf, sz)) == 0);

  /* Now your function should return NULL for the vector, covering the branch. */
  dd_ns(Policy_vec_t) v = plcs_get_policies(buf, sz);
  ASSERT_TRUE(v == NULL);
}
