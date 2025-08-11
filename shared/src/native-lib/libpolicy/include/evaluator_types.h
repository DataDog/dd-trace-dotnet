#pragma once

#include <evaluators_reader.h>

#include "dd_types.h"

#define STR_CMP_ENUM(name) TRANSLATE_ENUM(name, dd_ns(CmpTypeSTR))
#define NUM_CMP_ENUM(name) TRANSLATE_ENUM(name, dd_ns(CmpTypeNUM))
#define STR_EVAL(name) TRANSLATE_ENUM(name, dd_ns(StringEvaluators))
#define NUM_EVAL(name) TRANSLATE_ENUM(name, dd_ns(NumericEvaluators))

/**
 * @brief these are mappings between flatbuffers defined enums and a local
 * representation
 *
 */

typedef enum StringComparator {
  STR_CMP_ENUM(CMP_PREFIX),
  STR_CMP_ENUM(CMP_SUFFIX),
  STR_CMP_ENUM(CMP_CONTAINS),
  STR_CMP_ENUM(CMP_EXACT),
} StringComparator;

typedef enum NumericComparator {
  NUM_CMP_ENUM(CMP_EQ),
  NUM_CMP_ENUM(CMP_GT),
  NUM_CMP_ENUM(CMP_GTE),
  NUM_CMP_ENUM(CMP_LT),
  NUM_CMP_ENUM(CMP_LTE),
} NumericComparator;

typedef enum StringEvaluators {
  STR_EVAL(COMPONENT),
  STR_EVAL(PROCESS_EXE_PATH),
  STR_EVAL(PROCESS_BASEDIR_PATH),
  STR_EVAL(PROCESS_CWD),
  STR_EVAL(RUNTIME_LANGUAGE),
  STR_EVAL(RUNTIME_ENTRY_POINT_FILE),
  STR_EVAL(RUNTIME_ENTRY_POINT_CLASS),
  STR_EVAL(RUNTIME_ENTRY_POINT_PACKAGE),
  STR_EVAL(RUNTIME_VERSION),
  STR_EVAL(LIBC_FLAVOR),
  STR_EVAL(LIBC_VERSION),
  STR_EVAL(MACHINE_ARCHITECTURE),
  STR_EVAL(HOST_NAME),
  STR_EVAL(HOST_IP),
  STR_EVAL(OS),
  STR_EVAL(OS_DISTRO),
  STR_EVAL(OS_DISTRO_VERSION),
  STR_EVAL(OS_DISTRO_CODENAME),
  STR_EVAL(OS_KERNEL_VERSION),
  STR_EVAL(OS_USER),
  STR_EVAL(OS_USER_GROUP),
  STR_EVAL(CONTAINER_IMAGE),
  STR_EVAL(CONTAINER_ID),
  STR_EVAL(STR_EVAL_COUNT)
} StringEvaluators;

typedef enum NumericEvaluators {
  NUM_EVAL(JAVA_HEAP),
  NUM_EVAL(RUNTIME_VERSION_MAJOR),
  NUM_EVAL(RUNTIME_VERSION_MINOR),
  NUM_EVAL(RUNTIME_VERSION_PATCH),
  NUM_EVAL(OS_DISTRO_VERSION_MAJOR),
  NUM_EVAL(OS_DISTRO_VERSION_MINOR),
  NUM_EVAL(OS_DISTRO_VERSION_PATCH),
  NUM_EVAL(OS_KERNEL_VERSION_MAJOR),
  NUM_EVAL(OS_KERNEL_VERSION_MINOR),
  NUM_EVAL(OS_KERNEL_VERSION_PATCH),
  NUM_EVAL(LIBC_VERSION_MAJOR),
  NUM_EVAL(LIBC_VERSION_MINOR),
  NUM_EVAL(LIBC_VERSION_PATCH),
  NUM_EVAL(NUM_EVAL_COUNT)
} NumericEvaluators;

/**
 * @brief A signature for string evaluator functions.
 *
 */
typedef EvaluationResult (*string_evaluator_function_ptr)(
    const char *policy,
    const StringComparator cmp,
    const char *ctx,
    const char *description,
    StringEvaluators eval_id
);

/**
 * @brief A signature for numeric evaluator functions.
 *
 */
typedef EvaluationResult (*numeric_evaluator_function_ptr)(
    const long policy,
    const NumericComparator cmp,
    const long ctx,
    const char *description,
    NumericEvaluators eval_id
);

/**
 * @brief A signature for unsigned numeric evaluator functions.
 *
 */
typedef EvaluationResult (*unumeric_evaluator_function_ptr)(
    const unsigned long policy,
    const NumericComparator cmp,
    const unsigned long ctx,
    const char *description,
    NumericEvaluators eval_id
);

#undef STR_CMP_ENUM
#undef NUM_CMP_ENUM
#undef STR_EVAL
#undef NUM_EVAL
