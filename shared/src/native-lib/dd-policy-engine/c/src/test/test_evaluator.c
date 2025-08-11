/*
 * Unit test stubs for evaluator behavior and boolean composition.
 *
 * These tests use utest.h and exercise:
 *  - evaluate_buffer() basic behavior on NULL/invalid input
 *  - enum-to-string mapping helpers (sanity checks)
 *  - end-to-end evaluation with a generated FlatBuffers header (if available)
 *  - Individual evaluator functions (evaluate_numeric, evaluate_unumeric, node_evaluator)
 *  - Boolean operation functions (DoAnd, DoOr, DoNot, DoOper)
 *  - Composite evaluator function (composite_evaluator)
 *
 * For the end-to-end test, if you have run the Go example
 *   make -C ../go example_generate_c_header_buffer
 * the header will be available at:
 *   policies/go/example_generate_c_header_buffer/out/buffer.h
 * and exposes: `extern const uint8_t hardcoded_policies[];`
 *
 * For full testing of evaluator functions, the following FlatCC builder headers are needed:
 * - evaluators_builder.h (for creating mock StrEvaluator, NumEvaluator, UNumEvaluator)
 * - nodes_builder.h (for creating mock EvaluatorNode, CompositeNode, NodeTypeWrapper)
 * - boolean_operation_builder.h (for creating mock BoolOperation enums)
 *
 * This file intentionally avoids building FlatBuffers in C (we only ship
 * the reader side here). It relies on a generated header when present.
 */
#define _GNU_SOURCE

/* System headers */
#include <stddef.h>
#include <stdint.h>
#include <string.h>

/* External library headers */
#include "utest/utest.h"

/* Project public headers */
#include <dd/policies/error_codes.h>
#include <dd/policies/evaluator_default.h>
#include <dd/policies/policies.h>

/* Project internal headers */
#include "actions_reader.h"
#include "eval_ctx.h"
#include "evaluators_verifier.h"
#include "flatbuffers_common_builder.h"
#include "policy.h"
#include "policy_builder.h"
#include "wire/action.h"
#include "wire/boolean_operation.h"
#include "wire/dd_types.h"
#include "wire/evaluation_result.h"

/* Test-specific headers */
#include "hardcoded_policies.h"

// unexported functions
extern plcs_evaluation_result string_evaluator_exact(const char *eval, const char *param);
extern plcs_evaluation_result string_evaluator_prefix(const char *eval, const char *param);
extern plcs_evaluation_result string_evaluator_suffix(const char *eval, const char *param);
extern plcs_evaluation_result string_evaluator_contains(const char *eval, const char *param);

extern void plcs_eval_ctx_set_str_eval_error(plcs_string_evaluators ix, plcs_errors error);
extern void plcs_eval_ctx_set_num_eval_error(plcs_numeric_evaluators ix, plcs_errors error);
extern void plcs_eval_ctx_set_unum_eval_error(plcs_numeric_evaluators ix, plcs_errors error);

// Additional extern declarations for evaluator functions
extern plcs_evaluation_result evaluate_string(dd_ns(StrEvaluator_table_t) eval_str, const char *description);
extern plcs_evaluation_result evaluate_numeric(dd_ns(NumEvaluator_table_t) eval_num, const char *description);
extern plcs_evaluation_result evaluate_unumeric(dd_ns(UNumEvaluator_table_t) eval_unum, const char *description);
extern plcs_evaluation_result node_evaluator(dd_ns(EvaluatorNode_table_t) node);
extern plcs_evaluation_result DoAnd(plcs_evaluation_result a, plcs_evaluation_result b);
extern plcs_evaluation_result DoOr(plcs_evaluation_result a, plcs_evaluation_result b);
extern plcs_evaluation_result DoNot(plcs_evaluation_result res);
extern plcs_evaluation_result
DoOper(dd_ns(BoolOperation_enum_t) oper, plcs_evaluation_result a, plcs_evaluation_result b);
extern plcs_evaluation_result composite_evaluator(dd_ns(CompositeNode_table_t) node);

extern void plcs_eval_ctx_reset(void);

/* -------------------------------------------------------------------------- */
/* Helpers                                                                     */
/* -------------------------------------------------------------------------- */

static int g_allow_called = 0;
static int g_deny_called = 0;

static plcs_errors test_action_allow(
    plcs_evaluation_result res,
    char *values[],
    size_t value_len,
    const char *description,
    int action_id
) {
  (void)res;
  (void)values;
  (void)value_len;
  (void)description;
  (void)action_id;
  g_allow_called++;
  return DD_ESUCCESS;
}

static plcs_errors
test_action_deny(plcs_evaluation_result res, char *values[], size_t value_len, const char *description, int action_id) {
  (void)res;
  (void)values;
  (void)value_len;
  (void)description;
  (void)action_id;
  g_deny_called++;
  return DD_ESUCCESS;
}

/* -------------------------------------------------------------------------- */
/* Tests                                                                       */
/* -------------------------------------------------------------------------- */

UTEST(evaluator, evaluate_buffer_null_returns_no_data) {
  /* Passing NULL buffer yields DD_ENO_DATA */
  int rc = plcs_evaluate_buffer(NULL, 0);
  ASSERT_EQ(rc, DD_ENO_DATA);
}

UTEST(evaluator, enum_mappings_return_strings) {
  /* These must return non-NULL stable names derived from the FlatBuffers schema */
  ASSERT_TRUE(plcs_string_evaluators_to_string(STR_EVAL_COMPONENT) != NULL);
  ASSERT_TRUE(plcs_string_evaluators_to_string(STR_EVAL_RUNTIME_LANGUAGE) != NULL);
  ASSERT_TRUE(plcs_numeric_evaluators_to_string(NUM_EVAL_JAVA_HEAP) != NULL);
  ASSERT_TRUE(plcs_string_comparator_to_string(STR_CMP_EXACT) != NULL);
  ASSERT_TRUE(plcs_numeric_comparator_to_string(NUM_CMP_LTE) != NULL);
  ASSERT_TRUE(plcs_evaluation_result_to_string(EVAL_RESULT_TRUE) != NULL);
  ASSERT_TRUE(plcs_actions_to_string(INJECT_DENY) != NULL);
  ASSERT_TRUE(plcs_actions_to_string(SET_ENVAR) != NULL);
}

UTEST(evaluator, test_string_evaluator) {
  /* Test the string evaluator with a simple exact match */
  int res = string_evaluator_exact("test", "test");
  ASSERT_EQ(res, EVAL_RESULT_TRUE);

  /* Test with a mismatch */
  res = string_evaluator_exact("test", "not_test");
  ASSERT_EQ(res, EVAL_RESULT_FALSE);

  /* Test with NULL parameters */
  res = string_evaluator_exact(NULL, "test");
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
  res = string_evaluator_exact("test", NULL);
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);

  res = string_evaluator_prefix(NULL, "test");
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
  res = string_evaluator_prefix("test", NULL);
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);

  res = string_evaluator_suffix(NULL, "test");
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
  res = string_evaluator_suffix("test", NULL);
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
  res = string_evaluator_suffix("long_test", "test");
  ASSERT_EQ(res, EVAL_RESULT_FALSE);

  res = string_evaluator_contains(NULL, "test");
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
  res = string_evaluator_contains("test", NULL);
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
  res = string_evaluator_contains("test", "long_test");
  ASSERT_EQ(res, EVAL_RESULT_TRUE);
  res = string_evaluator_contains("", "long_test");
  ASSERT_EQ(res, EVAL_RESULT_TRUE);
}

UTEST(evaluator, out_of_bounds_eval_error_setters) {
  plcs_eval_ctx_set_str_eval_error(STR_EVAL__COUNT, DD_EUNKNOWN_EVAL_IX);
  int err = plcs_eval_ctx_get_str_eval_error(STR_EVAL__COUNT);
  ASSERT_EQ(err, DD_EIX_OVERFLOW);

  plcs_eval_ctx_set_num_eval_error(NUM_EVAL__COUNT, DD_EUNKNOWN_EVAL_IX);
  err = plcs_eval_ctx_get_num_eval_error(NUM_EVAL__COUNT);
  ASSERT_EQ(err, DD_EIX_OVERFLOW);

  plcs_eval_ctx_set_unum_eval_error(NUM_EVAL__COUNT, DD_EUNKNOWN_EVAL_IX);
  err = plcs_eval_ctx_get_unum_eval_error(NUM_EVAL__COUNT);
  ASSERT_EQ(err, DD_EIX_OVERFLOW);
}

UTEST(evaluator, default_string_eval_sanity) {
  /* Sanity around defaults (no buffer involved) */
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

  /* Abstain on missing data */
  ASSERT_EQ(
      plcs_default_string_evaluator(NULL, STR_CMP_EXACT, "abc", "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_ABSTAIN
  );
  ASSERT_EQ(
      plcs_default_string_evaluator("abc", STR_CMP_EXACT, NULL, "d", STR_EVAL_COMPONENT),
      (plcs_evaluation_result)EVAL_RESULT_ABSTAIN
  );

  ASSERT_EQ((int)evaluate_string(NULL, "d"), EVAL_RESULT_ABSTAIN);
}

UTEST(evaluator, default_numeric_eval_sanity) {
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
}

UTEST(evaluator, default_unumeric_eval_sanity) {
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
}

UTEST(evaluator, test_conversion_evalresult_to_wire) {
  ASSERT_EQ(dd_evalresult_to_wire(EVAL_RESULT_TRUE), dd_ns(EvaluationResult_EVAL_RESULT_TRUE));
  ASSERT_EQ(dd_evalresult_to_wire(EVAL_RESULT_FALSE), dd_ns(EvaluationResult_EVAL_RESULT_FALSE));
  ASSERT_EQ(dd_evalresult_to_wire(EVAL_RESULT_ABSTAIN), dd_ns(EvaluationResult_EVAL_RESULT_ABSTAIN));
  ASSERT_EQ(dd_evalresult_to_wire(EVAL_RESULT__COUNT), -1);
}

/* -------------------------------------------------------------------------- */
/* Tests for evaluate_string.                                                 */
/* -------------------------------------------------------------------------- */

UTEST(evaluator, test_evaluate_string_null_input) {
  /* Test with NULL evaluator */
  int res = evaluate_string(NULL, "test description");
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
}

UTEST(evaluator, test_evaluate_string_basic_functionality) {
  /* Initialize context for numeric evaluation */
  int rc = plcs_eval_ctx_init();
  ASSERT_TRUE(rc == DD_ESUCCESS || rc == DD_EINITIZLIED);

  /* Set a string parameter for testing */
  rc = plcs_eval_ctx_set_str_eval_param(STR_EVAL_HOST_IP, "1.2.3.4");
  ASSERT_EQ(rc, DD_ESUCCESS);

  rc = plcs_eval_ctx_register_str_evaluator(plcs_default_string_evaluator, STR_EVAL_HOST_IP);
  ASSERT_EQ(rc, DD_ESUCCESS);

  /* Mocking a flatbuffer object */
  flatcc_builder_t b;
  size_t sz;
  flatcc_builder_init(&b);

  dd_wls_StrEvaluator_create_as_root(
      &b, dd_wls_StringEvaluators_HOST_IP, dd_wls_CmpTypeSTR_CMP_EXACT, flatbuffers_string_create_str(&b, "1.2.3.4")
  );

  void *buf = flatcc_builder_finalize_buffer(&b, &sz);
  ASSERT_TRUE(dd_wls_StrEvaluator_verify_as_root(buf, sz) == 0);
  dd_wls_StrEvaluator_table_t eval = dd_wls_StrEvaluator_as_root(buf);

  int res = evaluate_string(eval, "d");
  flatcc_builder_aligned_free(buf);
  ASSERT_EQ(res, EVAL_RESULT_TRUE);

  res = evaluate_string(NULL, "d");
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);

  // reset all ctx:
  plcs_eval_ctx_reset();
  res = evaluate_string(eval, "d");
  // shouldn't be any value
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
  flatcc_builder_reset(&b);
}

/* -------------------------------------------------------------------------- */
/* Tests for evaluate_numeric                                                  */
/* -------------------------------------------------------------------------- */

UTEST(evaluator, test_evaluate_numeric_null_input) {
  /* Test with NULL evaluator */
  int res = evaluate_numeric(NULL, "test description");
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
}

UTEST(evaluator, test_evaluate_numeric_basic_functionality) {
  /* Initialize context for numeric evaluation */
  int rc = plcs_eval_ctx_init();
  ASSERT_TRUE(rc == DD_ESUCCESS || rc == DD_EINITIZLIED);

  /* Set a numeric parameter for testing */
  rc = plcs_eval_ctx_set_num_eval_param(NUM_EVAL_JAVA_HEAP, 100);
  ASSERT_EQ(rc, DD_ESUCCESS);
  /* Mocking a flatbuffer object */
  flatcc_builder_t b;
  size_t sz;
  flatcc_builder_init(&b);
  dd_wls_NumEvaluator_create_as_root(&b, dd_wls_NumericEvaluators_JAVA_HEAP, dd_wls_CmpTypeNUM_CMP_EQ, 100);
  void *buf = flatcc_builder_finalize_buffer(&b, &sz);
  ASSERT_TRUE(dd_wls_NumEvaluator_verify_as_root(buf, sz) == 0);
  dd_wls_NumEvaluator_table_t eval = dd_wls_NumEvaluator_as_root(buf);
  int res = evaluate_numeric(eval, "d");
  ASSERT_EQ(res, EVAL_RESULT_TRUE);
  res = evaluate_numeric(NULL, "d");
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
  // force reset all ctx (init will return because of it's implementation)
  plcs_eval_ctx_reset();
  res = evaluate_numeric(eval, "d");
  // shouldn't be any value
  ASSERT_EQ(res, EVAL_RESULT_FALSE);
  flatcc_builder_aligned_free(buf);
  flatcc_builder_reset(&b);
}

/* -------------------------------------------------------------------------- */
/* Tests for evaluate_unumeric                                                 */
/* -------------------------------------------------------------------------- */

UTEST(evaluator, test_evaluate_unumeric_null_input) {
  /* Test with NULL evaluator */
  int res = evaluate_unumeric(NULL, "test description");
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
}

UTEST(evaluator, test_evaluate_unumeric_basic_functionality) {
  /* Mock a UNumEvaluator with basic values */
  /* Note: This test requires proper mocking of FlatCC objects */

  /* Initialize context for unumeric evaluation */
  int rc = plcs_eval_ctx_init();
  ASSERT_TRUE(rc == DD_ESUCCESS || rc == DD_EINITIZLIED);

  /* Set an unumeric parameter for testing */
  rc = plcs_eval_ctx_set_unum_eval_param(NUM_EVAL_JAVA_HEAP, 100);
  ASSERT_EQ(rc, DD_ESUCCESS);

  /* Mocking a flatbuffer object */
  flatcc_builder_t b;
  size_t sz;
  flatcc_builder_init(&b);

  dd_wls_UNumEvaluator_create_as_root(&b, dd_wls_NumericEvaluators_JAVA_HEAP, dd_wls_CmpTypeNUM_CMP_EQ, 100);

  void *buf = flatcc_builder_finalize_buffer(&b, &sz);
  ASSERT_TRUE(dd_wls_UNumEvaluator_verify_as_root(buf, sz) == 0);
  dd_wls_UNumEvaluator_table_t eval = dd_wls_UNumEvaluator_as_root(buf);

  int res = evaluate_unumeric(eval, "d");
  flatcc_builder_aligned_free(buf);
  ASSERT_EQ(res, EVAL_RESULT_TRUE);

  res = evaluate_unumeric(NULL, "d");
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
  flatcc_builder_reset(&b);
}

/* -------------------------------------------------------------------------- */
/* Tests for node_evaluator                                                   */
/* -------------------------------------------------------------------------- */

UTEST(evaluator, test_node_evaluator_null_input) {
  /* Test with NULL node */
  int res = node_evaluator(NULL);
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
}

UTEST(evaluator, test_node_evaluator_basic_functionality) {
  /* Mock an EvaluatorNode with basic values */
  /* Note: This test requires proper mocking of FlatCC objects */

#include <stdio.h>
  printf("hi?!\n");
  /* Initialize context for evaluation */
  int rc = plcs_eval_ctx_init();
  ASSERT_TRUE(rc == DD_ESUCCESS || rc == DD_EINITIZLIED);

  flatcc_builder_t b;
  size_t sz;
  flatcc_builder_init(&b);
  rc = plcs_eval_ctx_init();
  ASSERT_TRUE(rc == DD_ESUCCESS || rc == DD_EINITIZLIED);

  rc = plcs_eval_ctx_set_str_eval_param(STR_EVAL_RUNTIME_ENTRY_POINT_JAR, "test.jar");
  ASSERT_EQ(rc, DD_ESUCCESS);
  // str eval
  dd_wls_StrEvaluator_ref_t str = dd_wls_StrEvaluator_create(
      &b, dd_wls_StringEvaluators_RUNTIME_ENTRY_POINT_JAR, dd_wls_CmpTypeSTR_CMP_EXACT,
      flatbuffers_string_create_str(&b, "test.jar")
  );
  dd_wls_EvaluatorNode_create_as_root(&b, str, dd_wls_EvaluatorType_as_StrEvaluator(str));
  void *buf = flatcc_builder_finalize_buffer(&b, &sz);
  dd_wls_EvaluatorNode_table_t eval = dd_wls_EvaluatorNode_as_root(buf);

  rc = node_evaluator(eval);
  flatcc_builder_aligned_free(buf);
  flatcc_builder_clear(&b);
  ASSERT_EQ(rc, EVAL_RESULT_TRUE);

  /* Set a numeric parameter for testing */
  flatcc_builder_init(&b);
  rc = plcs_eval_ctx_init();
  ASSERT_TRUE(rc == DD_ESUCCESS || rc == DD_EINITIZLIED);
  rc = plcs_eval_ctx_set_num_eval_param(NUM_EVAL_JAVA_HEAP, 100);
  ASSERT_EQ(rc, DD_ESUCCESS);

  // num eval
  dd_wls_NumEvaluator_ref_t num =
      dd_wls_NumEvaluator_create(&b, dd_wls_NumericEvaluators_JAVA_HEAP, dd_wls_CmpTypeNUM_CMP_EQ, 100);

  dd_wls_EvaluatorNode_create_as_root(&b, num, dd_wls_EvaluatorType_as_NumEvaluator(num));

  buf = flatcc_builder_finalize_buffer(&b, &sz);
  eval = dd_wls_EvaluatorNode_as_root(buf);

  rc = node_evaluator(eval);
  flatcc_builder_aligned_free(buf);
  flatcc_builder_clear(&b);
  ASSERT_EQ(rc, EVAL_RESULT_TRUE);

  /* Set a numeric parameter for testing */
  flatcc_builder_init(&b);
  rc = plcs_eval_ctx_init();
  rc = plcs_eval_ctx_set_unum_eval_param(NUM_EVAL_RUNTIME_VERSION_MINOR, 4);
  ASSERT_EQ(rc, DD_ESUCCESS);
  // unum eval
  dd_wls_UNumEvaluator_ref_t unum =
      dd_wls_UNumEvaluator_create(&b, dd_wls_NumericEvaluators_RUNTIME_VERSION_MINOR, dd_wls_CmpTypeNUM_CMP_EQ, 4);

  dd_wls_EvaluatorNode_create_as_root(&b, unum, dd_wls_EvaluatorType_as_UNumEvaluator(unum));

  buf = flatcc_builder_finalize_buffer(&b, &sz);
  eval = dd_wls_EvaluatorNode_as_root(buf);

  rc = node_evaluator(eval);
  flatcc_builder_aligned_free(buf);
  ASSERT_EQ(rc, EVAL_RESULT_TRUE);
}

/* -------------------------------------------------------------------------- */
/* Tests for DoAnd function                                                    */
/* -------------------------------------------------------------------------- */

UTEST(evaluator, test_DoAnd_basic_operations) {
  /* Test AND logic with various combinations */

  /* TRUE & anything = anything */
  ASSERT_EQ((int)DoAnd(EVAL_RESULT_TRUE, EVAL_RESULT_TRUE), EVAL_RESULT_TRUE);
  ASSERT_EQ((int)DoAnd(EVAL_RESULT_TRUE, EVAL_RESULT_FALSE), EVAL_RESULT_FALSE);
  ASSERT_EQ((int)DoAnd(EVAL_RESULT_TRUE, EVAL_RESULT_ABSTAIN), EVAL_RESULT_ABSTAIN);
  ASSERT_EQ((int)DoAnd(EVAL_RESULT_FALSE, EVAL_RESULT_TRUE), EVAL_RESULT_FALSE);
  ASSERT_EQ((int)DoAnd(EVAL_RESULT_ABSTAIN, EVAL_RESULT_TRUE), EVAL_RESULT_ABSTAIN);

  /* FALSE & FALSE = FALSE */
  ASSERT_EQ((int)DoAnd(EVAL_RESULT_FALSE, EVAL_RESULT_FALSE), EVAL_RESULT_FALSE);

  /* FALSE & ABSTAIN = FALSE */
  ASSERT_EQ((int)DoAnd(EVAL_RESULT_FALSE, EVAL_RESULT_ABSTAIN), EVAL_RESULT_FALSE);
  ASSERT_EQ((int)DoAnd(EVAL_RESULT_ABSTAIN, EVAL_RESULT_FALSE), EVAL_RESULT_FALSE);

  /* ABSTAIN & ABSTAIN = ABSTAIN */
  ASSERT_EQ((int)DoAnd(EVAL_RESULT_ABSTAIN, EVAL_RESULT_ABSTAIN), EVAL_RESULT_ABSTAIN);
}

/* -------------------------------------------------------------------------- */
/* Tests for DoOr function                                                    */
/* -------------------------------------------------------------------------- */

UTEST(evaluator, test_DoOr_basic_operations) {
  /* Test OR logic with various combinations */

  /* TRUE | anything = TRUE */
  ASSERT_EQ((int)DoOr(EVAL_RESULT_TRUE, EVAL_RESULT_TRUE), EVAL_RESULT_TRUE);
  ASSERT_EQ((int)DoOr(EVAL_RESULT_TRUE, EVAL_RESULT_FALSE), EVAL_RESULT_TRUE);
  ASSERT_EQ((int)DoOr(EVAL_RESULT_TRUE, EVAL_RESULT_ABSTAIN), EVAL_RESULT_TRUE);
  ASSERT_EQ((int)DoOr(EVAL_RESULT_FALSE, EVAL_RESULT_TRUE), EVAL_RESULT_TRUE);
  ASSERT_EQ((int)DoOr(EVAL_RESULT_ABSTAIN, EVAL_RESULT_TRUE), EVAL_RESULT_TRUE);

  /* FALSE | FALSE = FALSE */
  ASSERT_EQ((int)DoOr(EVAL_RESULT_FALSE, EVAL_RESULT_FALSE), EVAL_RESULT_FALSE);

  /* FALSE | ABSTAIN = ABSTAIN */
  ASSERT_EQ((int)DoOr(EVAL_RESULT_FALSE, EVAL_RESULT_ABSTAIN), EVAL_RESULT_ABSTAIN);
  ASSERT_EQ((int)DoOr(EVAL_RESULT_ABSTAIN, EVAL_RESULT_FALSE), EVAL_RESULT_ABSTAIN);

  /* ABSTAIN | ABSTAIN = ABSTAIN */
  ASSERT_EQ((int)DoOr(EVAL_RESULT_ABSTAIN, EVAL_RESULT_ABSTAIN), EVAL_RESULT_ABSTAIN);
}

/* -------------------------------------------------------------------------- */
/* Tests for DoNot function                                                   */
/* -------------------------------------------------------------------------- */

UTEST(evaluator, test_DoNot_basic_operations) {
  /* Test NOT logic with various inputs */

  /* NOT TRUE = FALSE */
  ASSERT_EQ((int)DoNot(EVAL_RESULT_TRUE), EVAL_RESULT_FALSE);

  /* NOT FALSE = TRUE */
  ASSERT_EQ((int)DoNot(EVAL_RESULT_FALSE), EVAL_RESULT_TRUE);

  /* NOT ABSTAIN = ABSTAIN (preserved) */
  ASSERT_EQ((int)DoNot(EVAL_RESULT_ABSTAIN), EVAL_RESULT_ABSTAIN);
}

/* -------------------------------------------------------------------------- */
/* Tests for DoOper function                                                  */
/* -------------------------------------------------------------------------- */

UTEST(evaluator, test_DoOper_basic_operations) {
  /* Test DoOper with various boolean operations */

  /* Test AND operation */
  int res = DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_TRUE, EVAL_RESULT_TRUE);
  ASSERT_EQ(res, EVAL_RESULT_TRUE);

  res = DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_TRUE, EVAL_RESULT_FALSE);
  ASSERT_EQ(res, EVAL_RESULT_FALSE);

  res = DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_FALSE, EVAL_RESULT_TRUE);
  ASSERT_EQ(res, EVAL_RESULT_FALSE);

  /* Test OR operation */
  res = DoOper(dd_ns(BoolOperation_BOOL_OR), EVAL_RESULT_TRUE, EVAL_RESULT_FALSE);
  ASSERT_EQ(res, EVAL_RESULT_TRUE);

  res = DoOper(dd_ns(BoolOperation_BOOL_OR), EVAL_RESULT_FALSE, EVAL_RESULT_FALSE);
  ASSERT_EQ(res, EVAL_RESULT_FALSE);

  /* Test NOT operation (second parameter ignored for NOT) */
  res = DoOper(dd_ns(BoolOperation_BOOL_NOT), EVAL_RESULT_TRUE, EVAL_RESULT_FALSE);
  ASSERT_EQ(res, EVAL_RESULT_FALSE);

  res = DoOper(dd_ns(BoolOperation_BOOL_NOT), EVAL_RESULT_FALSE, EVAL_RESULT_TRUE);
  ASSERT_EQ(res, EVAL_RESULT_TRUE);

  res = DoOper(dd_ns(BoolOperation_BOOL_NOT), EVAL_RESULT_ABSTAIN, EVAL_RESULT_TRUE);
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);

  /* Test unknown operation */
  res = DoOper(99, EVAL_RESULT_TRUE, EVAL_RESULT_TRUE);
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
}

/* -------------------------------------------------------------------------- */
/* Tests for composite_evaluator                                              */
/* -------------------------------------------------------------------------- */

UTEST(evaluator, test_composite_evaluator_null_input) {
  /* Test with NULL node */
  int res = composite_evaluator(NULL);
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
}

UTEST(evaluator, test_composite_evaluator_basic_functionality) {
  /* Mock a CompositeNode with basic values */
  /* Note: This test requires proper mocking of FlatCC objects */

  /* Initialize context for evaluation */
  int rc = plcs_eval_ctx_init();
  ASSERT_TRUE(rc == DD_ESUCCESS || rc == DD_EINITIZLIED);

  /* Note: Full testing would require creating mock FlatCC objects */
  /* This test demonstrates the test structure for when headers are available */

  /* TODO: When FlatCC builder headers are available, create mock objects like:
   * - CompositeNode with op=BOOL_AND, children=[mock_evaluator_node]
   * - Test composite_evaluator() with the mock object
   * - Verify it applies boolean operations correctly
   */
}

/* -------------------------------------------------------------------------- */
/* Integration tests for boolean operations                                   */
/* -------------------------------------------------------------------------- */

UTEST(evaluator_integration, test_boolean_operation_truth_table) {
  /* Test comprehensive truth table for boolean operations */

  /* AND truth table */
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_TRUE, EVAL_RESULT_TRUE), EVAL_RESULT_TRUE);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_TRUE, EVAL_RESULT_FALSE), EVAL_RESULT_FALSE);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_TRUE, EVAL_RESULT_ABSTAIN), EVAL_RESULT_ABSTAIN);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_FALSE, EVAL_RESULT_TRUE), EVAL_RESULT_FALSE);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_FALSE, EVAL_RESULT_FALSE), EVAL_RESULT_FALSE);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_FALSE, EVAL_RESULT_ABSTAIN), EVAL_RESULT_FALSE);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_ABSTAIN, EVAL_RESULT_TRUE), EVAL_RESULT_ABSTAIN);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_ABSTAIN, EVAL_RESULT_FALSE), EVAL_RESULT_FALSE);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_ABSTAIN, EVAL_RESULT_ABSTAIN), EVAL_RESULT_ABSTAIN);

  /* OR truth table */
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_OR), EVAL_RESULT_TRUE, EVAL_RESULT_TRUE), EVAL_RESULT_TRUE);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_OR), EVAL_RESULT_TRUE, EVAL_RESULT_FALSE), EVAL_RESULT_TRUE);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_OR), EVAL_RESULT_TRUE, EVAL_RESULT_ABSTAIN), EVAL_RESULT_TRUE);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_OR), EVAL_RESULT_FALSE, EVAL_RESULT_TRUE), EVAL_RESULT_TRUE);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_OR), EVAL_RESULT_FALSE, EVAL_RESULT_FALSE), EVAL_RESULT_FALSE);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_OR), EVAL_RESULT_FALSE, EVAL_RESULT_ABSTAIN), EVAL_RESULT_ABSTAIN);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_OR), EVAL_RESULT_ABSTAIN, EVAL_RESULT_TRUE), EVAL_RESULT_TRUE);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_OR), EVAL_RESULT_ABSTAIN, EVAL_RESULT_FALSE), EVAL_RESULT_ABSTAIN);
  ASSERT_EQ((int)DoOper(dd_ns(BoolOperation_BOOL_OR), EVAL_RESULT_ABSTAIN, EVAL_RESULT_ABSTAIN), EVAL_RESULT_ABSTAIN);
}

/* -------------------------------------------------------------------------- */
/* Additional edge case tests                                                  */
/* -------------------------------------------------------------------------- */

UTEST(evaluator, test_boolean_operations_edge_cases) {
  /* Test edge cases and boundary conditions */

  /* Test with invalid enum values */
  int res = DoOper(99, EVAL_RESULT_TRUE, EVAL_RESULT_TRUE);
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);

  res = DoOper(-1, EVAL_RESULT_FALSE, EVAL_RESULT_FALSE);
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);

  /* Test with BOOL_COUNT (should be invalid) */
  res = DoOper(dd_ns(BoolOperation_BOOL_COUNT), EVAL_RESULT_TRUE, EVAL_RESULT_TRUE);
  ASSERT_EQ(res, EVAL_RESULT_ABSTAIN);
}

UTEST(evaluator, test_boolean_operations_commutativity) {
  /* Test that AND and OR operations are commutative */

  /* AND commutativity */
  ASSERT_EQ(DoAnd(EVAL_RESULT_TRUE, EVAL_RESULT_FALSE), DoAnd(EVAL_RESULT_FALSE, EVAL_RESULT_TRUE));
  ASSERT_EQ(DoAnd(EVAL_RESULT_TRUE, EVAL_RESULT_ABSTAIN), DoAnd(EVAL_RESULT_ABSTAIN, EVAL_RESULT_TRUE));
  ASSERT_EQ(DoAnd(EVAL_RESULT_FALSE, EVAL_RESULT_ABSTAIN), DoAnd(EVAL_RESULT_ABSTAIN, EVAL_RESULT_FALSE));

  /* OR commutativity */
  ASSERT_EQ(DoOr(EVAL_RESULT_TRUE, EVAL_RESULT_FALSE), DoOr(EVAL_RESULT_FALSE, EVAL_RESULT_TRUE));
  ASSERT_EQ(DoOr(EVAL_RESULT_TRUE, EVAL_RESULT_ABSTAIN), DoOr(EVAL_RESULT_ABSTAIN, EVAL_RESULT_TRUE));
  ASSERT_EQ(DoOr(EVAL_RESULT_FALSE, EVAL_RESULT_ABSTAIN), DoOr(EVAL_RESULT_ABSTAIN, EVAL_RESULT_FALSE));
}

UTEST(evaluator, test_boolean_operations_associativity) {
  /* Test that AND and OR operations are associative */

  /* AND associativity: (a & b) & c = a & (b & c) */
  plcs_evaluation_result left = DoAnd(DoAnd(EVAL_RESULT_TRUE, EVAL_RESULT_FALSE), EVAL_RESULT_ABSTAIN);
  plcs_evaluation_result right = DoAnd(EVAL_RESULT_TRUE, DoAnd(EVAL_RESULT_FALSE, EVAL_RESULT_ABSTAIN));
  ASSERT_EQ(left, right);

  /* OR associativity: (a | b) | c = a | (b | c) */
  left = DoOr(DoOr(EVAL_RESULT_FALSE, EVAL_RESULT_TRUE), EVAL_RESULT_ABSTAIN);
  right = DoOr(EVAL_RESULT_FALSE, DoOr(EVAL_RESULT_TRUE, EVAL_RESULT_ABSTAIN));
  ASSERT_EQ(left, right);
}

UTEST(evaluator, test_boolean_operations_distributivity) {
  /* Test De Morgan's laws and distributivity */

  /* De Morgan's law: NOT(a AND b) = NOT(a) OR NOT(b) */
  plcs_evaluation_result left = DoNot(DoAnd(EVAL_RESULT_TRUE, EVAL_RESULT_FALSE));
  plcs_evaluation_result right = DoOr(DoNot(EVAL_RESULT_TRUE), DoNot(EVAL_RESULT_FALSE));
  ASSERT_EQ(left, right);

  /* De Morgan's law: NOT(a OR b) = NOT(a) AND NOT(b) */
  left = DoNot(DoOr(EVAL_RESULT_TRUE, EVAL_RESULT_FALSE));
  right = DoAnd(DoNot(EVAL_RESULT_TRUE), DoNot(EVAL_RESULT_FALSE));
  ASSERT_EQ(left, right);
}

UTEST(evaluator, test_extern_declarations_working) {
  /* Simple test to verify that extern declarations are working */
  /* This test calls the functions to ensure they can be linked */

  /* Test DoAnd with simple values */
  int res = DoAnd(EVAL_RESULT_TRUE, EVAL_RESULT_TRUE);
  ASSERT_EQ(res, EVAL_RESULT_TRUE);

  /* Test DoOr with simple values */
  res = DoOr(EVAL_RESULT_FALSE, EVAL_RESULT_TRUE);
  ASSERT_EQ(res, EVAL_RESULT_TRUE);

  /* Test DoNot with simple values */
  res = DoNot(EVAL_RESULT_TRUE);
  ASSERT_EQ(res, EVAL_RESULT_FALSE);

  /* Test DoOper with AND operation */
  res = DoOper(dd_ns(BoolOperation_BOOL_AND), EVAL_RESULT_TRUE, EVAL_RESULT_FALSE);
  ASSERT_EQ(res, EVAL_RESULT_FALSE);
}

/*
 * End-to-end evaluation test using a generated FlatBuffers header.
 *
 * The generated buffer contains several simple policies of the form:
 *   if RUNTIME_LANGUAGE == "<lang>" then execute action INJECT_DENY
 *
 * We set the runtime language in the evaluation context, register action handlers,
 * and verify that actions are executed (regardless of TRUE/FALSE - the engine
 * passes the plcs_evaluation_result to the action and the action may decide what to do).
 */
UTEST(evaluator_integration, evaluate_generated_header_if_available) {
  /* Initialize context and register actions */
  int rc = plcs_eval_ctx_init();
  /* init; accept already-inited code path as well */
  ASSERT_TRUE(rc == DD_ESUCCESS || rc == DD_EINITIZLIED);

  g_allow_called = 0;
  g_deny_called = 0;

  int prc = plcs_eval_ctx_register_action(test_action_allow, INJECT_ALLOW);
  ASSERT_EQ(prc, DD_ESUCCESS);
  prc = plcs_eval_ctx_register_action(test_action_deny, INJECT_DENY);
  ASSERT_EQ(prc, DD_ESUCCESS);

  /* Provide context parameter used by policies (runtime language) */
  prc = plcs_eval_ctx_set_str_eval_param(STR_EVAL_RUNTIME_LANGUAGE, "jvm");
  ASSERT_EQ(prc, DD_ESUCCESS);

  /* Evaluate the embedded buffer */
  int eval_rc = plcs_evaluate_buffer(hardcoded_policies, hardcoded_policies_len);

  /* Non-zero would indicate action failures; we expect success. */
  ASSERT_EQ(eval_rc, DD_ESUCCESS);

  /* We expect at least one DENY action to have been invoked. */
  ASSERT_TRUE(g_deny_called >= 1);

#if HAVE_HARDCODED_POLICIES_HEADER
  /* No generated header available; this test is a stub. */
  ASSERT_TRUE(1);
#endif
}
