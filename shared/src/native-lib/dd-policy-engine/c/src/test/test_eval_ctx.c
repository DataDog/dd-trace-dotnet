/*
 * Unit test stubs for eval_ctx module using utest.h
 *
 * These tests exercise basic registration, parameter setting, getters,
 * bounds checking, and a couple of default evaluator sanity checks.
 *
 * Build system is expected to compile this alongside test.c
 * which provides UTEST_MAIN().
 */
#define _GNU_SOURCE
#include "utest/utest.h"

#include <dd/policies/policies.h>

#include "eval_ctx.h"

#include <stddef.h>
#include <stdint.h>
#include <string.h>

/* -------------------------------------------------------------------------- */
/* Dummy evaluators and action for testing                                     */
/* -------------------------------------------------------------------------- */

static plcs_evaluation_result dummy_str_eval(
    const char *policy,
    const plcs_string_comparator cmp,
    const char *ctx,
    const char *description,
    plcs_string_evaluators eval_id
) {
  (void)cmp;
  (void)description;
  (void)eval_id;

  if (!policy || !ctx) {
    return EVAL_RESULT_ABSTAIN;
  }
  return (strcmp(policy, ctx) == 0) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
}

static plcs_evaluation_result dummy_num_eval(
    const long policy,
    const plcs_numeric_comparator cmp,
    const long ctx,
    const char *description,
    plcs_numeric_evaluators eval_id
) {
  (void)cmp;
  (void)description;
  (void)eval_id;
  return (policy == ctx) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
}

static plcs_evaluation_result dummy_unum_eval(
    const unsigned long policy,
    const plcs_numeric_comparator cmp,
    const unsigned long ctx,
    const char *description,
    plcs_numeric_evaluators eval_id
) {
  (void)cmp;
  (void)description;
  (void)eval_id;
  return (policy == ctx) ? EVAL_RESULT_TRUE : EVAL_RESULT_FALSE;
}

static int g_action_called = 0;
static plcs_errors
dummy_action(plcs_evaluation_result res, char *values[], size_t value_len, const char *description, int action_id) {
  (void)res;
  (void)values;
  (void)value_len;
  (void)description;
  (void)action_id;
  g_action_called++;
  return DD_ESUCCESS;
}

/* -------------------------------------------------------------------------- */
/* Tests                                                                       */
/* -------------------------------------------------------------------------- */

UTEST(eval_ctx, eval_ctx_init_double_init) {
  int r1 = plcs_eval_ctx_init();
  int r2 = plcs_eval_ctx_init();
  /* Depending on previous tests, r1 may already be -DD_EINITIZLIED. */
  ASSERT_TRUE((r1 == DD_ESUCCESS && r2 == DD_EINITIZLIED) || (r1 == DD_EINITIZLIED && r2 == DD_EINITIZLIED));
}

UTEST(eval_ctx, verify_error_handling_string_evaluator_and_param) {
  int rc = plcs_eval_ctx_register_str_evaluator(NULL, STR_EVAL_RUNTIME_LANGUAGE);
  ASSERT_EQ(rc, DD_EREGISTER_EVAL_PTR);

  rc = plcs_eval_ctx_register_str_evaluator(dummy_str_eval, STR_EVAL__COUNT);
  ASSERT_EQ(rc, DD_EIX_OVERFLOW);

  const char *param = plcs_eval_ctx_get_string_param(STR_EVAL__COUNT);
  ASSERT_TRUE(param == STR_NOT_SET);

  int err = plcs_eval_ctx_get_last_error();
  ASSERT_EQ(err, DD_EIX_OVERFLOW);
}

UTEST(eval_ctx, register_and_get_string_evaluator_and_param) {
  /* Ensure initialized */
  (void)plcs_eval_ctx_init();

  const char *ctx_value = "jvm";
  int rc = plcs_eval_ctx_register_str_evaluator(dummy_str_eval, STR_EVAL_RUNTIME_LANGUAGE);
  ASSERT_EQ(rc, DD_ESUCCESS);

  rc = plcs_eval_ctx_set_str_eval_param(STR_EVAL_RUNTIME_LANGUAGE, ctx_value);
  ASSERT_EQ(rc, DD_ESUCCESS);

  plcs_string_evaluator_function_ptr f = plcs_eval_ctx_get_string_evaluator(STR_EVAL_RUNTIME_LANGUAGE);
  ASSERT_TRUE(f != NULL);

  const char *param = plcs_eval_ctx_get_string_param(STR_EVAL__COUNT);
  ASSERT_TRUE(param == STR_NOT_SET);
  int err = plcs_eval_ctx_get_last_error();
  ASSERT_EQ(err, DD_EIX_OVERFLOW);

  param = plcs_eval_ctx_get_string_param(STR_EVAL_RUNTIME_LANGUAGE);
  ASSERT_TRUE(param != NULL);
  ASSERT_EQ(0, strcmp(param, ctx_value));

  /* Ensure evaluator behaves as expected */
  int r = f("jvm", STR_CMP_EXACT, param, "desc", STR_EVAL_RUNTIME_LANGUAGE);
  ASSERT_EQ(r, EVAL_RESULT_TRUE);

  r = f("python", STR_CMP_EXACT, param, "desc", STR_EVAL_RUNTIME_LANGUAGE);
  ASSERT_EQ(r, EVAL_RESULT_FALSE);
}

UTEST(eval_ctx, verify_error_handling_numeric_evaluator_and_param) {
  int rc = plcs_eval_ctx_register_num_evaluator(NULL, NUM_EVAL_JAVA_HEAP);
  ASSERT_EQ(rc, DD_EREGISTER_EVAL_PTR);

  rc = plcs_eval_ctx_register_num_evaluator(dummy_num_eval, NUM_EVAL__COUNT);
  ASSERT_EQ(rc, DD_EIX_OVERFLOW);

  long param = plcs_eval_ctx_get_numeric_param(NUM_EVAL__COUNT);
  ASSERT_TRUE(param == NUM_NOT_SET);

  int err = plcs_eval_ctx_get_last_error();
  ASSERT_EQ(err, DD_EIX_OVERFLOW);
}

UTEST(eval_ctx, register_and_get_numeric_evaluator_and_param) {
  (void)plcs_eval_ctx_init();

  long ctx_value = 42;
  int rc = plcs_eval_ctx_register_num_evaluator(dummy_num_eval, NUM_EVAL_JAVA_HEAP);
  ASSERT_EQ(rc, DD_ESUCCESS);

  rc = plcs_eval_ctx_register_num_evaluator(NULL, NUM_EVAL_JAVA_HEAP);
  ASSERT_EQ(rc, DD_EREGISTER_EVAL_PTR);

  rc = plcs_eval_ctx_register_num_evaluator(dummy_num_eval, NUM_EVAL__COUNT);
  ASSERT_EQ(rc, DD_EIX_OVERFLOW);

  rc = plcs_eval_ctx_set_num_eval_param(NUM_EVAL_JAVA_HEAP, ctx_value);
  ASSERT_EQ(rc, DD_ESUCCESS);

  rc = plcs_eval_ctx_set_num_eval_param(NUM_EVAL__COUNT, ctx_value);
  ASSERT_EQ(rc, DD_EIX_OVERFLOW);

  plcs_numeric_evaluator_function_ptr f = plcs_eval_ctx_get_numeric_evaluator(NUM_EVAL_JAVA_HEAP);
  ASSERT_TRUE(f != NULL);

  long param = plcs_eval_ctx_get_numeric_param(NUM_EVAL_JAVA_HEAP);
  ASSERT_EQ(param, ctx_value);

  int r = f(42, NUM_CMP_EQ, param, "desc", NUM_EVAL_JAVA_HEAP);
  ASSERT_EQ(r, EVAL_RESULT_TRUE);

  r = f(7, NUM_CMP_EQ, param, "desc", NUM_EVAL_JAVA_HEAP);
  ASSERT_EQ(r, EVAL_RESULT_FALSE);
}

UTEST(eval_ctx, verify_error_handling_unumeric_evaluator_and_param) {
  int rc = plcs_eval_ctx_register_unum_evaluator(NULL, NUM_EVAL_JAVA_HEAP);
  ASSERT_EQ(rc, DD_EREGISTER_EVAL_PTR);

  rc = plcs_eval_ctx_register_unum_evaluator(dummy_unum_eval, NUM_EVAL__COUNT);
  ASSERT_EQ(rc, DD_EIX_OVERFLOW);

  unsigned long param = plcs_eval_ctx_get_unumeric_param(NUM_EVAL__COUNT);
  ASSERT_TRUE(param == UNUM_NOT_SET);

  int err = plcs_eval_ctx_get_last_error();
  ASSERT_EQ(err, DD_EIX_OVERFLOW);
}

UTEST(eval_ctx, register_and_get_unumeric_evaluator_and_param) {
  (void)plcs_eval_ctx_init();

  unsigned long ctx_value = 7ul;
  int rc = plcs_eval_ctx_register_unum_evaluator(dummy_unum_eval, NUM_EVAL_RUNTIME_VERSION_MAJOR);
  ASSERT_EQ(rc, DD_ESUCCESS);

  rc = plcs_eval_ctx_register_unum_evaluator(NULL, NUM_EVAL_RUNTIME_VERSION_MAJOR);
  ASSERT_EQ(rc, DD_EREGISTER_EVAL_PTR);

  rc = plcs_eval_ctx_register_unum_evaluator(dummy_unum_eval, NUM_EVAL__COUNT);
  ASSERT_EQ(rc, DD_EIX_OVERFLOW);

  rc = plcs_eval_ctx_set_unum_eval_param(NUM_EVAL_RUNTIME_VERSION_MAJOR, ctx_value);
  ASSERT_EQ(rc, DD_ESUCCESS);

  rc = plcs_eval_ctx_set_unum_eval_param(NUM_EVAL__COUNT, ctx_value);
  ASSERT_EQ(rc, DD_EIX_OVERFLOW);

  plcs_unumeric_evaluator_function_ptr f = plcs_eval_ctx_get_unumeric_evaluator(NUM_EVAL_RUNTIME_VERSION_MAJOR);
  ASSERT_TRUE(f != NULL);

  unsigned long param = plcs_eval_ctx_get_unumeric_param(NUM_EVAL_RUNTIME_VERSION_MAJOR);
  ASSERT_EQ(param, ctx_value);

  int r = f(7ul, NUM_CMP_EQ, param, "desc", NUM_EVAL_RUNTIME_VERSION_MAJOR);
  ASSERT_EQ(r, EVAL_RESULT_TRUE);

  r = f(8ul, NUM_CMP_EQ, param, "desc", NUM_EVAL_RUNTIME_VERSION_MAJOR);
  ASSERT_EQ(r, EVAL_RESULT_FALSE);
}

UTEST(eval_ctx, register_and_invoke_action_pointer) {
  (void)plcs_eval_ctx_init();

  int rc = plcs_eval_ctx_register_action(dummy_action, INJECT_ALLOW);
  ASSERT_EQ(rc, DD_ESUCCESS);

  rc = plcs_eval_ctx_register_action(dummy_action, ACTIONS__COUNT);
  ASSERT_EQ(rc, DD_EIX_OVERFLOW);

  plcs_action_function_ptr act = plcs_eval_ctx_get_action(INJECT_ALLOW);
  ASSERT_TRUE(act != NULL);

  g_action_called = 0;
  char *vals[] = {(char *)"v1", (char *)"v2"};
  rc = act(EVAL_RESULT_TRUE, vals, 2, "desc", INJECT_ALLOW);
  ASSERT_EQ(rc, DD_ESUCCESS);
  ASSERT_EQ(g_action_called, 1);
}

UTEST(eval_ctx, bounds_checks_and_error_reporting) {
  (void)plcs_eval_ctx_init();

  /* Out of range register attempts should overflow */
  int rc = plcs_eval_ctx_register_str_evaluator(dummy_str_eval, (plcs_string_evaluators)STR_EVAL__COUNT);
  ASSERT_EQ(rc, DD_EIX_OVERFLOW);

  rc = plcs_eval_ctx_set_str_eval_param((plcs_string_evaluators)STR_EVAL__COUNT, "x");
  ASSERT_EQ(rc, DD_EIX_OVERFLOW);

  /* Getter with OOB should set last error to DD_EIX_OVERFLOW */
  plcs_string_evaluator_function_ptr f = plcs_eval_ctx_get_string_evaluator((plcs_string_evaluators)STR_EVAL__COUNT);
  ASSERT_TRUE(f == NULL);
  int err = plcs_eval_ctx_get_last_error();
  ASSERT_EQ(err, DD_EIX_OVERFLOW);

  /* Same for numeric */
  plcs_numeric_evaluator_function_ptr nf =
      plcs_eval_ctx_get_numeric_evaluator((plcs_numeric_evaluators)NUM_EVAL__COUNT);
  ASSERT_TRUE(nf == NULL);
  err = plcs_eval_ctx_get_last_error();
  ASSERT_EQ(err, DD_EIX_OVERFLOW);

  /* And unsigned numeric */
  plcs_unumeric_evaluator_function_ptr unf =
      plcs_eval_ctx_get_unumeric_evaluator((plcs_numeric_evaluators)NUM_EVAL__COUNT);
  ASSERT_TRUE(unf == NULL);
  err = plcs_eval_ctx_get_last_error();
  ASSERT_EQ(err, DD_EIX_OVERFLOW);

  /* And actions */
  plcs_action_function_ptr act = plcs_eval_ctx_get_action((plcs_actions)ACTIONS__COUNT);
  ASSERT_TRUE(act == NULL);
  err = plcs_eval_ctx_get_last_error();
  ASSERT_EQ(err, DD_EIX_OVERFLOW);
}

UTEST(eval_ctx, last_error_set_and_get) {
  (void)plcs_eval_ctx_init();

  plcs_eval_ctx_set_error(DD_EUNKNOWN_CMP);
  ASSERT_EQ(plcs_eval_ctx_peek_last_error(), (plcs_errors)DD_EUNKNOWN_CMP);
  ASSERT_EQ(plcs_eval_ctx_get_last_error(), (plcs_errors)DD_EUNKNOWN_CMP);
  /* After get_last_error(), it resets to success */
  ASSERT_EQ(plcs_eval_ctx_peek_last_error(), (plcs_errors)DD_ESUCCESS);
}

UTEST(eval_ctx, set_error_out_of_bound) {
  plcs_eval_ctx_set_action_error(ACTIONS__COUNT, 0);
  int err = plcs_eval_ctx_get_last_error();
  ASSERT_EQ(err, DD_ESUCCESS);
}

/* -------------------------------------------------------------------------- */
/* Default evaluator sanity checks                                             */
/* -------------------------------------------------------------------------- */

UTEST(default_evaluators, string_comparators) {
  ASSERT_EQ(
      plcs_default_string_evaluator("abc", STR_CMP_EXACT, "abc", "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );
  ASSERT_EQ(
      plcs_default_string_evaluator("ab", STR_CMP_PREFIX, "abc", "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );
  ASSERT_EQ(
      plcs_default_string_evaluator("bc", STR_CMP_SUFFIX, "abc", "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );
  ASSERT_EQ(
      plcs_default_string_evaluator("b", STR_CMP_CONTAINS, "abc", "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );

  ASSERT_EQ(
      plcs_default_string_evaluator("abc", STR_CMP_EXACT, "abcd", "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );
  ASSERT_EQ(
      plcs_default_string_evaluator("ac", STR_CMP_PREFIX, "abc", "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );
  ASSERT_EQ(
      plcs_default_string_evaluator("ac", STR_CMP_SUFFIX, "abc", "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );
  ASSERT_EQ(
      plcs_default_string_evaluator("z", STR_CMP_CONTAINS, "abc", "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );

  /* ABSTAIN on missing data */
  ASSERT_EQ(
      plcs_default_string_evaluator(NULL, STR_CMP_EXACT, "abc", "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_ABSTAIN
  );
  ASSERT_EQ(
      plcs_default_string_evaluator("abc", STR_CMP_EXACT, NULL, "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_ABSTAIN
  );

  /* ERRORS */
  ASSERT_EQ(
      plcs_default_string_evaluator("abc", STR_CMP__COUNT, "a", "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_ABSTAIN
  );
  ASSERT_EQ(
      plcs_default_string_evaluator("abc", STR_CMP__COUNT + 1, "a", "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_ABSTAIN
  );
  int err = plcs_eval_ctx_get_str_eval_error(STR_EVAL_COMPONENT);
  ASSERT_EQ(err, DD_EUNKNOWN_CMP);

  err = plcs_eval_ctx_get_str_eval_error(STR_EVAL__COUNT);
  ASSERT_EQ(err, DD_EIX_OVERFLOW);
}

UTEST(default_evaluators, numeric_comparators) {
  ASSERT_EQ(
      plcs_default_numeric_evaluator(5, NUM_CMP_EQ, 5, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );
  ASSERT_EQ(
      plcs_default_numeric_evaluator(6, NUM_CMP_GT, 5, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );
  ASSERT_EQ(
      plcs_default_numeric_evaluator(5, NUM_CMP_GTE, 5, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );
  ASSERT_EQ(
      plcs_default_numeric_evaluator(4, NUM_CMP_LT, 5, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );
  ASSERT_EQ(
      plcs_default_numeric_evaluator(5, NUM_CMP_LTE, 5, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );

  ASSERT_EQ(
      plcs_default_numeric_evaluator(4, NUM_CMP_EQ, 5, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );
  ASSERT_EQ(
      plcs_default_numeric_evaluator(4, NUM_CMP_GT, 5, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );
  ASSERT_EQ(
      plcs_default_numeric_evaluator(6, NUM_CMP_GTE, 7, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );
  ASSERT_EQ(
      plcs_default_numeric_evaluator(6, NUM_CMP_LT, 5, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );
  ASSERT_EQ(
      plcs_default_numeric_evaluator(6, NUM_CMP_LTE, 5, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );

  /* ERRORS */
  ASSERT_EQ(
      plcs_default_numeric_evaluator(1, NUM_CMP__COUNT, 2, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_ABSTAIN
  );
  ASSERT_EQ(
      plcs_default_numeric_evaluator(1, NUM_CMP__COUNT + 1, 2, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_ABSTAIN
  );
  int err = plcs_eval_ctx_get_num_eval_error(NUM_EVAL_JAVA_HEAP);
  ASSERT_EQ(err, DD_EUNKNOWN_CMP);

  err = plcs_eval_ctx_get_num_eval_error(NUM_EVAL__COUNT);
  ASSERT_EQ(err, DD_EIX_OVERFLOW);
}

UTEST(default_evaluators, unumeric_comparators) {
  ASSERT_EQ(
      plcs_default_unumeric_evaluator(5ul, NUM_CMP_EQ, 5ul, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );
  ASSERT_EQ(
      plcs_default_unumeric_evaluator(6ul, NUM_CMP_GT, 5ul, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );
  ASSERT_EQ(
      plcs_default_unumeric_evaluator(5ul, NUM_CMP_GTE, 5ul, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );
  ASSERT_EQ(
      plcs_default_unumeric_evaluator(4ul, NUM_CMP_LT, 5ul, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );
  ASSERT_EQ(
      plcs_default_unumeric_evaluator(5ul, NUM_CMP_LTE, 5ul, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_TRUE
  );

  ASSERT_EQ(
      plcs_default_unumeric_evaluator(4ul, NUM_CMP_EQ, 5ul, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );
  ASSERT_EQ(
      plcs_default_unumeric_evaluator(4ul, NUM_CMP_GT, 5ul, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );
  ASSERT_EQ(
      plcs_default_unumeric_evaluator(6ul, NUM_CMP_GTE, 7ul, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );
  ASSERT_EQ(
      plcs_default_unumeric_evaluator(6ul, NUM_CMP_LT, 5ul, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );
  ASSERT_EQ(
      plcs_default_unumeric_evaluator(6ul, NUM_CMP_LTE, 5ul, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_FALSE
  );

  /* ERRORS */
  ASSERT_EQ(
      plcs_default_unumeric_evaluator(1, NUM_CMP__COUNT, 2, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_ABSTAIN
  );
  ASSERT_EQ(
      plcs_default_unumeric_evaluator(1, NUM_CMP__COUNT + 1, 2, "d", NUM_EVAL_JAVA_HEAP),
      (plcs_evaluation_result)EVAL_RESULT_ABSTAIN
  );
  int err = plcs_eval_ctx_get_unum_eval_error(NUM_EVAL_JAVA_HEAP);
  ASSERT_EQ(err, DD_EUNKNOWN_CMP);

  err = plcs_eval_ctx_get_unum_eval_error(NUM_EVAL__COUNT);
  ASSERT_EQ(err, DD_EIX_OVERFLOW);
}
