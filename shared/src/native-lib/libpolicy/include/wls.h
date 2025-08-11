/*
 * C++ bindings for the Datadog's Policy Engine
 *
 * This header defines the core public API for initializing and interacting with the Policy Engine.
 * It provides mechanisms to set evaluation parameters, register evaluators and actions,
 * and evaluate policy buffers or files.
 */
#pragma once

#include <filesystem>
extern "C" {
#include <error_codes.h>
#include <evaluator.h>
#include <policy.h>
#include "action.h"
}

#include <cstdint>
#include <functional>
#include <iostream>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

namespace datadog::wls {

// TODO(@dmehala): Expose X-macros in the C public header to avoid code duplication
// and ensure consistency across all enum values.
enum class Result : int {
  TTRUE = RESULT_TRUE,
  FFALSE = RESULT_FALSE,
  ABSTAIN = RESULT_ABSTAIN,
};

enum class StringEvaluator : int {
  COMPONENT,
  PROCESS_EXE_PATH,
  PROCESS_BASEDIR_PATH,
  PROCESS_CWD,
  RUNTIME_LANGUAGE,
  RUNTIME_ENTRY_POINT_FILE,
  RUNTIME_ENTRY_POINT_CLASS,
  RUNTIME_ENTRY_POINT_PACKAGE,
  RUNTIME_VERSION,
  LIBC_FLAVOR,
  LIBC_VERSION,
  MACHINE_ARCHITECTURE,
  HOST_NAME,
  HOST_IP,
  OS,
  OS_DISTRO,
  OS_DISTRO_VERSION,
  OS_DISTRO_CODENAME,
  OS_KERNEL_VERSION,
  OS_USER,
  OS_USER_GROUP,
  CONTAINER_IMAGE,
  CONTAINER_ID,
  STR_EVAL_COUNT
};

enum class NumericEvaluator : int {
  JAVA_HEAP,
  RUNTIME_VERSION_MAJOR,
  RUNTIME_VERSION_MINOR,
  RUNTIME_VERSION_PATCH,
  OS_DISTRO_VERSION_MAJOR,
  OS_DISTRO_VERSION_MINOR,
  OS_DISTRO_VERSION_PATCH,
  OS_KERNEL_VERSION_MAJOR,
  OS_KERNEL_VERSION_MINOR,
  OS_KERNEL_VERSION_PATCH,
  LIBC_VERSION_MAJOR,
  LIBC_VERSION_MINOR,
  LIBC_VERSION_PATCH,
  NUM_EVAL_COUNT
};

enum class Action : int {
  INJECT_DENY,
  INJECT_ALLOW,
};

enum class Error : int {
  EREGISTER_EVAL_PTR = 1,
  EIX_OVERFLOW,
  ENULL_PTR,
  EINITIALIZED,
  ENO_DATA,
  EUNKNOWN_EVAL_IX,
  EACTIONS_EVAL,
  EUNKNOWN_CMP,
};

using StringEvaluatorFunc = std::function<Result(const char *, const char *, const char *)>;
using NumericEvaluatorFunc = std::function<Result(const long policy, const long ctx, const char *desc)>;
using ActionFunc = std::function<std::optional<Error>(Result, const std::vector<const char *> &, const char *)>;

std::ostream &operator<<(std::ostream &os, Result res);

/// @brief Initializes the policy engine.
///
/// Must be called before using any other API functions.
/// Sets up internal state and prepares the engine for configuration and evaluation.
void init();

/// @brief Sets a numeric evaluation parameter.
///
/// @param evaluator The numeric evaluator key.
/// @param value The `long` value to associate with the evaluator.
void set_params(NumericEvaluator, long);

/// @brief Sets a numeric evaluation parameter.
///
/// @param evaluator The numeric evaluator key.
/// @param value The `unsigned long` value to associate with the evaluator.
void set_params(NumericEvaluator, unsigned long);

/// @brief Sets a string evaluation parameter.
///
/// @param evaluator The string evaluator key.
/// @param value The `std::string_view` to associate with the evaluator.
void set_params(StringEvaluator, std::string_view);

/// @brief Registers a string evaluator function.
///
/// @param evaluator The string evaluator key.
/// @param func The evaluator function to register for the key.
void register_evaluator(StringEvaluator, StringEvaluatorFunc);

/// @brief Registers a numeric evaluator function.
///
/// @param evaluator The numeric evaluator key.
/// @param func The evaluator function to register for the key.
void register_evaluator(NumericEvaluator, NumericEvaluatorFunc);

/// @brief Registers a policy action function.
///
/// @param action The action identifier.
/// @param func The function to invoke when this action is triggered.
void register_action(Action, ActionFunc);

/// @brief Evaluates a policy from a raw buffer.
///
/// @param buffer A vector of bytes representing the serialized policy.
/// @return An optional `Result`. Returns `std::nullopt` if evaluation fails or is invalid.
std::optional<Result> evaluate_buffer(const std::vector<uint8_t> &buffer);

/// @brief Evaluates a policy from a file.
///
/// @param path The filesystem path to the policy file.
/// @return An optional `Result`. Returns `std::nullopt` if file read or evaluation fails.
std::optional<Result> evaluate_buffer_from_file(const std::filesystem::path &);

}  // namespace datadog::wls
