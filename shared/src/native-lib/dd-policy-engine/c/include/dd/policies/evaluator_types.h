#pragma once

#include "evaluation_result.h"

/**
 * @brief these are public represntations of the wire (flatbuffers) enums used in the policy engine
 */

/**
 * @brief comparison operators for string evaluators
 */
typedef enum plcs_string_comparator {
  STR_CMP_PREFIX = 0,
  STR_CMP_SUFFIX = 1,
  STR_CMP_CONTAINS = 2,
  STR_CMP_EXACT = 3,
  STR_CMP__COUNT
} plcs_string_comparator;

/**
 * @brief comparison operators for numeric (and unsigned numeric) evaluators
 */
typedef enum plcs_numeric_comparator {
  NUM_CMP_EQ = 0,
  NUM_CMP_GT = 1,
  NUM_CMP_GTE = 2,
  NUM_CMP_LT = 3,
  NUM_CMP_LTE = 4,
  NUM_CMP__COUNT
} plcs_numeric_comparator;

/**
 * @brief string evaluators
 * These represent the supported string evaluators by the policy engine.
 */
typedef enum plcs_string_evaluators {
  STR_EVAL_COMPONENT = 0,
  STR_EVAL_PROCESS_EXE = 1,
  STR_EVAL_PROCESS_EXE_FULL_PATH = 2,
  STR_EVAL_PROCESS_BASEDIR_PATH = 3,
  STR_EVAL_PROCESS_ARGV = 4,
  STR_EVAL_PROCESS_CWD = 5,
  STR_EVAL_RUNTIME_LANGUAGE = 6,
  STR_EVAL_RUNTIME_ENTRY_POINT_FILE = 7,
  STR_EVAL_RUNTIME_ENTRY_POINT_JAR = 8,
  STR_EVAL_RUNTIME_ENTRY_POINT_CLASS = 9,
  STR_EVAL_RUNTIME_ENTRY_POINT_PACKAGE = 10,
  STR_EVAL_RUNTIME_ENTRY_POINT_MODULE = 11,
  STR_EVAL_RUNTIME_ENTRY_POINT_SOURCE = 12,
  STR_EVAL_RUNTIME_DOPTION = 13,
  STR_EVAL_RUNTIME_VERSION = 14,
  STR_EVAL_LIBC_FLAVOR = 15,
  STR_EVAL_LIBC_VERSION = 16,
  STR_EVAL_MACHINE_ARCHITECTURE = 17,
  STR_EVAL_HOST_NAME = 18,
  STR_EVAL_HOST_IP = 19,
  STR_EVAL_OS = 20,
  STR_EVAL_OS_DISTRO = 21,
  STR_EVAL_OS_DISTRO_VERSION = 22,
  STR_EVAL_OS_DISTRO_CODENAME = 23,
  STR_EVAL_OS_KERNEL_VERSION = 24,
  STR_EVAL_OS_KERNEL_NAME = 25,
  STR_EVAL_OS_USER = 26,
  STR_EVAL_OS_USER_GROUP = 27,
  STR_EVAL_CONTAINER_IMAGE = 28,
  STR_EVAL_CONTAINER_ID = 29,
  STR_EVAL_ALWAYS_TRUE = 30,
  STR_EVAL_ALWAYS_FALSE = 31,
  STR_EVAL_ALWAYS_ABSTAIN = 32,
  STR_EVAL__COUNT
} plcs_string_evaluators;

/**
 * @brief numeric evaluators
 * These represent the supported numeric evaluators by the policy engine.
 */
typedef enum plcs_numeric_evaluators {
  NUM_EVAL_JAVA_HEAP = 0,
  NUM_EVAL_RUNTIME_VERSION_MAJOR = 1,
  NUM_EVAL_RUNTIME_VERSION_MINOR = 2,
  NUM_EVAL_RUNTIME_VERSION_PATCH = 3,
  NUM_EVAL_OS_DISTRO_VERSION_MAJOR = 4,
  NUM_EVAL_OS_DISTRO_VERSION_MINOR = 5,
  NUM_EVAL_OS_DISTRO_VERSION_PATCH = 6,
  NUM_EVAL_OS_KERNEL_VERSION_MAJOR = 7,
  NUM_EVAL_OS_KERNEL_VERSION_MINOR = 8,
  NUM_EVAL_OS_KERNEL_VERSION_PATCH = 9,
  NUM_EVAL_LIBC_VERSION_MAJOR = 10,
  NUM_EVAL_LIBC_VERSION_MINOR = 11,
  NUM_EVAL_LIBC_VERSION_PATCH = 12,
  NUM_EVAL__COUNT
} plcs_numeric_evaluators;

/**
 * @brief A signature for string evaluator functions.
 *
 */
typedef plcs_evaluation_result (*plcs_string_evaluator_function_ptr)(
    const char *policy,
    const plcs_string_comparator cmp,
    const char *ctx,
    const char *description,
    plcs_string_evaluators eval_id
);

/**
 * @brief A signature for numeric evaluator functions.
 *
 */
typedef plcs_evaluation_result (*plcs_numeric_evaluator_function_ptr)(
    const long policy,
    const plcs_numeric_comparator cmp,
    const long ctx,
    const char *description,
    plcs_numeric_evaluators eval_id
);

/**
 * @brief A signature for unsigned numeric evaluator functions.
 *
 */
typedef plcs_evaluation_result (*plcs_unumeric_evaluator_function_ptr)(
    const unsigned long policy,
    const plcs_numeric_comparator cmp,
    const unsigned long ctx,
    const char *description,
    plcs_numeric_evaluators eval_id
);

/**
 * @brief Converts a plcs_string_evaluators enum to a string representation.
 * @param eval_id The plcs_string_evaluators enum value.
 * @return A string representation of the evaluator.
 */
const char *plcs_string_evaluators_to_string(enum plcs_string_evaluators eval_id);

/**
 * @brief Converts a plcs_numeric_evaluators enum to a string representation.
 * @param eval_id The plcs_numeric_evaluators enum value.
 * @return A string representation of the evaluator.
 */
const char *plcs_numeric_evaluators_to_string(enum plcs_numeric_evaluators eval_id);

/**
 * @brief Converts a plcs_string_comparator enum to a string representation.
 * @param cmp The plcs_string_comparator enum value.
 * @return A string representation of the comparator.
 */
const char *plcs_string_comparator_to_string(enum plcs_string_comparator cmp);

/**
 * @brief Converts a plcs_numeric_comparator enum to a string representation.
 * @param cmp The plcs_numeric_comparator enum value.
 * @return A string representation of the comparator.
 */
const char *plcs_numeric_comparator_to_string(enum plcs_numeric_comparator cmp);
