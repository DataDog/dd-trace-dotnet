#include <string.h>

#include <dd/policies/error_codes.h>
#include <dd/policies/eval_ctx.h>
#include <dd/policies/evaluator_default.h>

#include "eval_ctx.h"

#define DD_UNUSED(x) (void)x

/**
 * @brief A string comparison evaluator that checks if the evaluated string
 * matches the provided parameter exactly
 * @param eval The [policy] value
 * @param param The [ctx] parameter to compare against
 * @return EVAL_RESULT_ABSTAIN if data is corrupt or non existent, EVAL_RESULT_TRUE if the
 * strings match, EVAL_RESULT_FALSE otherwise
 */
plcs_evaluation_result string_evaluator_exact(const char *eval, const char *param) {
  if (!eval || !param) {
    return EVAL_RESULT_ABSTAIN;  // 'dont-care' state
  }

  return strncmp(eval, param, strlen(eval) + 1) == 0 ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
}

/**
 * @brief A string comparison evaluator that checks if the evaluated string
 * matches the provided parameter as a prefix
 * @param eval The [policy] value
 * @param param The [ctx] parameter to compare against
 * @return EVAL_RESULT_ABSTAIN if data is corrupt or non existent, EVAL_RESULT_TRUE if the
 * strings match, EVAL_RESULT_FALSE otherwise
 */
plcs_evaluation_result string_evaluator_prefix(const char *eval, const char *param) {
  if (!eval || !param) {
    return EVAL_RESULT_ABSTAIN;  // 'dont-care' state
  }

  for (; *eval; ++eval, ++param) {
    if (*eval != *param)
      return EVAL_RESULT_FALSE;
  }
  return EVAL_RESULT_TRUE;
}

/**
 * @brief A string comparison evaluator that checks if the evaluated string
 * matches the provided parameter as a suffix
 * @param eval The [policy] value
 * @param param The [ctx] parameter to compare against
 * @return EVAL_RESULT_ABSTAIN if data is corrupt or non existent, EVAL_RESULT_TRUE if the
 * strings match, EVAL_RESULT_FALSE otherwise
 */
plcs_evaluation_result string_evaluator_suffix(const char *eval, const char *param) {
  if (!eval || !param) {
    return EVAL_RESULT_ABSTAIN;  // 'dont-care' state
  }

  size_t eval_len = strlen(eval);
  size_t param_len = strlen(param);

  if (eval_len > param_len) {
    return EVAL_RESULT_FALSE;  // eval is longer than param, cannot be suffix
  }

  for (size_t i = 0U; i < eval_len; ++i) {
    if (param[param_len - eval_len + i] != eval[i]) {
      return EVAL_RESULT_FALSE;
    }
  }

  return EVAL_RESULT_TRUE;
}

plcs_evaluation_result string_evaluator_contains(const char *eval, const char *param) {
  if (!eval || !param) {
    return EVAL_RESULT_ABSTAIN;  // 'dont-care' state
  }

  size_t eval_len = strlen(eval);
  size_t param_len = strlen(param);

  if (eval_len > param_len) {
    return EVAL_RESULT_FALSE;  // eval is longer than param, cannot be contained in
                               // param
  }

  if (eval_len == 0) {
    return EVAL_RESULT_TRUE;  // empty string is contained in any string
  }

  return (strstr(param, eval) != NULL) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
}

plcs_evaluation_result plcs_default_string_evaluator(
    const char *policy,
    const plcs_string_comparator cmp,
    const char *ctx,
    const char *description,
    plcs_string_evaluators eval_id
) {
  DD_UNUSED(description);
  DD_UNUSED(eval_id);

  if (!policy) {
    // log error?
    return EVAL_RESULT_ABSTAIN;
  }

  if (!ctx) {
    // log error?
    return EVAL_RESULT_ABSTAIN;
  }

  switch (cmp) {
    case STR_CMP_EXACT:
      return string_evaluator_exact(policy, ctx);
      break;

    case STR_CMP_PREFIX:
      return string_evaluator_prefix(policy, ctx);
      break;

    case STR_CMP_SUFFIX:
      return string_evaluator_suffix(policy, ctx);
      break;

    case STR_CMP_CONTAINS:
      return string_evaluator_contains(policy, ctx);
      break;

    case STR_CMP__COUNT:
      // error we should not get here!
      return EVAL_RESULT_ABSTAIN;
      break;
  }

  plcs_eval_ctx_set_str_eval_error(eval_id, DD_EUNKNOWN_CMP);
  return EVAL_RESULT_ABSTAIN;
}

plcs_evaluation_result plcs_default_numeric_evaluator(
    const long policy,
    const plcs_numeric_comparator cmp,
    const long ctx,
    const char *description,
    plcs_numeric_evaluators eval_id
) {
  DD_UNUSED(description);
  DD_UNUSED(eval_id);

  switch (cmp) {
    case NUM_CMP_EQ:
      return (policy == ctx) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
      break;

    case NUM_CMP_GT:
      return (policy > ctx) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
      break;

    case NUM_CMP_GTE:
      return (policy >= ctx) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
      break;

    case NUM_CMP_LT:
      return (policy < ctx) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
      break;

    case NUM_CMP_LTE:
      return (policy <= ctx) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
      break;

    case NUM_CMP__COUNT:
      // error we should not get here!
      return EVAL_RESULT_ABSTAIN;
      break;
  }

  plcs_eval_ctx_set_num_eval_error(eval_id, DD_EUNKNOWN_CMP);
  return EVAL_RESULT_ABSTAIN;
}

plcs_evaluation_result plcs_default_unumeric_evaluator(
    const unsigned long policy,
    const plcs_numeric_comparator cmp,
    const unsigned long ctx,
    const char *description,
    plcs_numeric_evaluators eval_id
) {
  DD_UNUSED(description);
  DD_UNUSED(eval_id);

  switch (cmp) {
    case NUM_CMP_EQ:
      return (policy == ctx) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
      break;

    case NUM_CMP_GT:
      return (policy > ctx) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
      break;

    case NUM_CMP_GTE:
      return (policy >= ctx) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
      break;

    case NUM_CMP_LT:
      return (policy < ctx) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
      break;

    case NUM_CMP_LTE:
      return (policy <= ctx) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
      break;

    case NUM_CMP__COUNT:
      // error we should not get here!
      return EVAL_RESULT_ABSTAIN;
      break;
  }

  plcs_eval_ctx_set_unum_eval_error(eval_id, DD_EUNKNOWN_CMP);
  return EVAL_RESULT_ABSTAIN;
}

#undef DD_UNUSED
