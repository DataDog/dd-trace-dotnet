/**
 * @file wire/evaluation_types.h
 * @brief Internal enum translation layer for the library.
 *
 * @details
 * This header bridges our public-facing enums in `../../include/evaluation_types.h`
 * with the on-the-wire / generated enum values from the FlatBuffers definitions (via `evaluators_reader.h`).
 *
 * It provides static inline helpers to:
 *   - Translate public enum values to the vendor/FlatBuffers integer values
 *   - Translate vendor/FlatBuffers integer values back to public enums
 *   - Sanity check table sizes against enum counts at compile time
 *
 * @note
 * This is **private** library glue — it is not installed, not exported,
 * and must not appear in any public-facing headers.
 * If you are writing code outside the library, include the public header
 * from `../../include/` instead and use only the public enum names.
 *
 * We own the public enum names and values; upstream values may change,
 * and this file is where we adapt to those changes.
 *
 * Guidelines:
 *   - No clever macros in public headers
 *   - Use `_Static_assert` and `-Wswitch-enum` to catch drift early
 *   - Update translation tables and asserts when public or vendor enums change
 *
 * @todo write a script that generate these files
 */
#pragma once

/* PRIVATE header: only used when building the library. Not installed. */

#include <dd/policies/evaluator_types.h>
#include <evaluators_reader.h> /* flatbuffers generated headers */
#include "dd_types.h"          /* dd_ns(...) */

#ifdef __cplusplus
extern "C" {
#endif

static inline dd_ns(CmpTypeSTR_enum_t) dd_strcmp_to_wire(enum plcs_string_comparator v) {
  /* Map your public enum -> vendor numeric value. */
  static const int map[STR_CMP__COUNT] = {
      [STR_CMP_PREFIX] = dd_ns(CmpTypeSTR_CMP_PREFIX),
      [STR_CMP_SUFFIX] = dd_ns(CmpTypeSTR_CMP_SUFFIX),
      [STR_CMP_CONTAINS] = dd_ns(CmpTypeSTR_CMP_CONTAINS),
      [STR_CMP_EXACT] = dd_ns(CmpTypeSTR_CMP_EXACT),
  };
  _Static_assert(
      STR_CMP__COUNT == dd_ns(CmpTypeSTR_CMP_COUNT),
      "update dd_strcmp_to_wire & plcs_string_comparator mappings when you modify CmpTypeSTR"
  );
  return (dd_ns(CmpTypeSTR_enum_t))((unsigned)v < STR_CMP__COUNT ? map[v] : -1);
}

static inline dd_ns(CmpTypeNUM_enum_t) dd_numcmp_to_wire(enum plcs_numeric_comparator v) {
  static const int map[NUM_CMP__COUNT] = {
      [NUM_CMP_EQ] = dd_ns(CmpTypeNUM_CMP_EQ),   [NUM_CMP_GT] = dd_ns(CmpTypeNUM_CMP_GT),
      [NUM_CMP_GTE] = dd_ns(CmpTypeNUM_CMP_GTE), [NUM_CMP_LT] = dd_ns(CmpTypeNUM_CMP_LT),
      [NUM_CMP_LTE] = dd_ns(CmpTypeNUM_CMP_LTE),
  };
  _Static_assert(
      NUM_CMP__COUNT == dd_ns(CmpTypeNUM_CMP_COUNT),
      "update dd_numcmp_to_wire & plcs_numeric_comparator mappings when you modify CmpTypeNUM"
  );
  return (dd_ns(CmpTypeNUM_enum_t))((unsigned)v < NUM_CMP__COUNT ? map[v] : -1);
}

static inline dd_ns(StringEvaluators_enum_t) dd_streval_to_wire(enum plcs_string_evaluators v) {
  /* Keep indices aligned with your public enum order. */
  static const int map[STR_EVAL__COUNT] = {
      [STR_EVAL_COMPONENT] = dd_ns(StringEvaluators_COMPONENT),
      [STR_EVAL_PROCESS_EXE] = dd_ns(StringEvaluators_PROCESS_EXE),
      [STR_EVAL_PROCESS_EXE_FULL_PATH] = dd_ns(StringEvaluators_PROCESS_EXE_FULL_PATH),
      [STR_EVAL_PROCESS_BASEDIR_PATH] = dd_ns(StringEvaluators_PROCESS_BASEDIR_PATH),
      [STR_EVAL_PROCESS_ARGV] = dd_ns(StringEvaluators_PROCESS_ARGV),
      [STR_EVAL_PROCESS_CWD] = dd_ns(StringEvaluators_PROCESS_CWD),
      [STR_EVAL_RUNTIME_LANGUAGE] = dd_ns(StringEvaluators_RUNTIME_LANGUAGE),
      [STR_EVAL_RUNTIME_ENTRY_POINT_FILE] = dd_ns(StringEvaluators_RUNTIME_ENTRY_POINT_FILE),
      [STR_EVAL_RUNTIME_ENTRY_POINT_JAR] = dd_ns(StringEvaluators_RUNTIME_ENTRY_POINT_JAR),
      [STR_EVAL_RUNTIME_ENTRY_POINT_CLASS] = dd_ns(StringEvaluators_RUNTIME_ENTRY_POINT_CLASS),
      [STR_EVAL_RUNTIME_ENTRY_POINT_PACKAGE] = dd_ns(StringEvaluators_RUNTIME_ENTRY_POINT_PACKAGE),
      [STR_EVAL_RUNTIME_ENTRY_POINT_MODULE] = dd_ns(StringEvaluators_RUNTIME_ENTRY_POINT_MODULE),
      [STR_EVAL_RUNTIME_ENTRY_POINT_SOURCE] = dd_ns(StringEvaluators_RUNTIME_ENTRY_POINT_SOURCE),
      [STR_EVAL_RUNTIME_DOPTION] = dd_ns(StringEvaluators_RUNTIME_DOPTION),
      [STR_EVAL_RUNTIME_VERSION] = dd_ns(StringEvaluators_RUNTIME_VERSION),
      [STR_EVAL_LIBC_FLAVOR] = dd_ns(StringEvaluators_LIBC_FLAVOR),
      [STR_EVAL_LIBC_VERSION] = dd_ns(StringEvaluators_LIBC_VERSION),
      [STR_EVAL_MACHINE_ARCHITECTURE] = dd_ns(StringEvaluators_MACHINE_ARCHITECTURE),
      [STR_EVAL_HOST_NAME] = dd_ns(StringEvaluators_HOST_NAME),
      [STR_EVAL_HOST_IP] = dd_ns(StringEvaluators_HOST_IP),
      [STR_EVAL_OS] = dd_ns(StringEvaluators_OS),
      [STR_EVAL_OS_DISTRO] = dd_ns(StringEvaluators_OS_DISTRO),
      [STR_EVAL_OS_DISTRO_VERSION] = dd_ns(StringEvaluators_OS_DISTRO_VERSION),
      [STR_EVAL_OS_DISTRO_CODENAME] = dd_ns(StringEvaluators_OS_DISTRO_CODENAME),
      [STR_EVAL_OS_KERNEL_VERSION] = dd_ns(StringEvaluators_OS_KERNEL_VERSION),
      [STR_EVAL_OS_KERNEL_NAME] = dd_ns(StringEvaluators_OS_KERNEL_NAME),
      [STR_EVAL_OS_USER] = dd_ns(StringEvaluators_OS_USER),
      [STR_EVAL_OS_USER_GROUP] = dd_ns(StringEvaluators_OS_USER_GROUP),
      [STR_EVAL_CONTAINER_IMAGE] = dd_ns(StringEvaluators_CONTAINER_IMAGE),
      [STR_EVAL_CONTAINER_ID] = dd_ns(StringEvaluators_CONTAINER_ID),
      [STR_EVAL_ALWAYS_TRUE] = dd_ns(StringEvaluators_ALWAYS_TRUE),
      [STR_EVAL_ALWAYS_FALSE] = dd_ns(StringEvaluators_ALWAYS_FALSE),
      [STR_EVAL_ALWAYS_ABSTAIN] = dd_ns(StringEvaluators_ALWAYS_ABSTAIN)
  };
  _Static_assert(
      STR_EVAL__COUNT == dd_ns(StringEvaluators_STR_EVAL_COUNT),
      "update dd_streval_to_wire & plcs_string_evaluators mappings when you modify StringEvaluators"
  );
  return (dd_ns(StringEvaluators_enum_t))((unsigned)v < STR_EVAL__COUNT ? map[v] : -1);
}

static inline dd_ns(NumericEvaluators_enum_t) dd_numeval_to_wire(enum plcs_numeric_evaluators v) {
  static const int map[NUM_EVAL__COUNT] = {
      [NUM_EVAL_JAVA_HEAP] = dd_ns(NumericEvaluators_JAVA_HEAP),
      [NUM_EVAL_RUNTIME_VERSION_MAJOR] = dd_ns(NumericEvaluators_RUNTIME_VERSION_MAJOR),
      [NUM_EVAL_RUNTIME_VERSION_MINOR] = dd_ns(NumericEvaluators_RUNTIME_VERSION_MINOR),
      [NUM_EVAL_RUNTIME_VERSION_PATCH] = dd_ns(NumericEvaluators_RUNTIME_VERSION_PATCH),
      [NUM_EVAL_OS_DISTRO_VERSION_MAJOR] = dd_ns(NumericEvaluators_OS_DISTRO_VERSION_MAJOR),
      [NUM_EVAL_OS_DISTRO_VERSION_MINOR] = dd_ns(NumericEvaluators_OS_DISTRO_VERSION_MINOR),
      [NUM_EVAL_OS_DISTRO_VERSION_PATCH] = dd_ns(NumericEvaluators_OS_DISTRO_VERSION_PATCH),
      [NUM_EVAL_OS_KERNEL_VERSION_MAJOR] = dd_ns(NumericEvaluators_OS_KERNEL_VERSION_MAJOR),
      [NUM_EVAL_OS_KERNEL_VERSION_MINOR] = dd_ns(NumericEvaluators_OS_KERNEL_VERSION_MINOR),
      [NUM_EVAL_OS_KERNEL_VERSION_PATCH] = dd_ns(NumericEvaluators_OS_KERNEL_VERSION_PATCH),
      [NUM_EVAL_LIBC_VERSION_MAJOR] = dd_ns(NumericEvaluators_LIBC_VERSION_MAJOR),
      [NUM_EVAL_LIBC_VERSION_MINOR] = dd_ns(NumericEvaluators_LIBC_VERSION_MINOR),
      [NUM_EVAL_LIBC_VERSION_PATCH] = dd_ns(NumericEvaluators_LIBC_VERSION_PATCH),
  };
  _Static_assert(
      NUM_EVAL__COUNT == dd_ns(NumericEvaluators_NUM_EVAL_COUNT),
      "update dd_numeval_to_wire & plcs_numeric_evaluators mappings when you modify NumericEvaluators"
  );
  return (dd_ns(NumericEvaluators_enum_t))((unsigned)v < NUM_EVAL__COUNT ? map[v] : -1);
}

#ifdef __cplusplus
}
#endif
