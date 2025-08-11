
#include "policy.h"
#include <dd/policies/error_codes.h>
#include <dd/policies/eval_ctx.h>
#include "wire/action.h"
#include "wire/dd_types.h"

#include <policy_verifier.h>

#include <stdio.h>

dd_ns(Policy_vec_t) plcs_get_policies(const uint8_t *buffer, size_t size) {
  if (!buffer) {
    // errlog
    return NULL;
  }

  // should be enough to verify the whole object;
  // A buffer can be verified to ensure it does not contain any ranges that point outside the the given buffer size,
  // that all data structures are aligned according to the flatbuffer principles, that strings are zero terminated,
  // and that required fields are present.
  // https://github.com/dvidelabs/flatcc?tab=readme-ov-file#verifying-a-buffer
  int ret = dd_ns(Policies_verify_as_root(buffer, size));
  if (ret) {
    flatcc_verify_error_string(ret);
    return NULL;
  }
  dd_ns(Policies_table_t) policies_root = dd_ns(Policies_as_root)(buffer);
  if (!policies_root) {
    // we shouldn't reach here following a successful verify
    // LCOV_EXCL_START
    // GCOVR_EXCL_START
    return NULL;
    // LCOV_EXCL_STOP
    // GCOVR_EXCL_STOP
  }
  dd_ns(Policy_vec_t) policies = dd_ns(Policies_policies)(policies_root);
  if (!policies) {
    // errlog
    return NULL;
  }
  return policies;
}
