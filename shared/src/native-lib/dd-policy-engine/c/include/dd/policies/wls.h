/*
 * C++ bindings for the Datadog's Policy Engine
 *
 * This header defines the core public API for initializing and interacting with the Policy Engine.
 * It provides mechanisms to set evaluation parameters, register evaluators and actions,
 * and evaluate policy buffers or files.
 */
#pragma once

extern "C" {
#include "dd/policies/action.h"
#include "dd/policies/error_codes.h"
#include "dd/policies/eval_ctx.h"
#include "dd/policies/evaluation_result.h"
#include "dd/policies/evaluator_types.h"
#include "dd/policies/policies.h"
}

#include <cstdint>
#include <functional>
#include <iostream>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

#ifndef _MSC_VER
#include <fstream>
#else
#include <cstdio>
#endif
#include <array>

#include <filesystem>

namespace datadog::wls {

// TODO(@dmehala): Expose X-macros in the C public header to avoid code duplication
// and ensure consistency across all enum values.
enum class Result : int {
  TTRUE = EVAL_RESULT_TRUE,
  FFALSE = EVAL_RESULT_FALSE,
  ABSTAIN = EVAL_RESULT_ABSTAIN,
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

namespace {

template <typename T, size_t N>
constexpr auto make_empty_array() -> std::array<T, N> {
  std::array<T, N> res;
  res.fill(nullptr);
  return res;
}

// NOTE(@dmehala): Temporary workaround because the API doesn't offer the possibility to store additional context.
auto actions_callback = make_empty_array<ActionFunc, ACTIONS__COUNT>();
auto string_evaluators_callback = make_empty_array<StringEvaluatorFunc, STR_EVAL__COUNT>();
auto numeric_evaluators_callback = make_empty_array<NumericEvaluatorFunc, NUM_EVAL__COUNT>();

plcs_evaluation_result on_str_evaluator(
    const char *policy,
    const plcs_string_comparator,
    const char *ctx,
    const char *desc,
    plcs_string_evaluators id
) {
  auto callback = string_evaluators_callback[id];
  /*if (callback == nullptr)*/
  /*  return RESULT_ABSTAIN;*/

  return static_cast<plcs_evaluation_result>(callback(policy, ctx, desc));
}

plcs_evaluation_result on_numeric_evaluator(
    const long policy,
    const plcs_numeric_comparator,
    const long ctx,
    const char *desc,
    plcs_numeric_evaluators id
) {
  auto callback = numeric_evaluators_callback[id];
  /*if (callback == nullptr)*/
  /*  return RESULT_ABSTAIN;*/

  return static_cast<plcs_evaluation_result>(callback(policy, ctx, desc));
}

plcs_errors
on_actions(plcs_evaluation_result result, char *values[], size_t values_len, const char *desc, int action_id) {
  auto callback = actions_callback[action_id];
  /*if (callback == nullptr)*/
  /*  return DD_EUNKNOWN_EVAL_IX;*/

  auto v = std::vector<const char *>{};
  v.reserve(values_len);
  for (auto i = 0; i < values_len; ++i) {
    v.emplace_back(values[i]);
  }

  auto maybe_error = callback(static_cast<Result>(result), v, desc);
  return maybe_error ? static_cast<plcs_errors>(*maybe_error) : DD_ESUCCESS;
}

}  // namespace

std::ostream &operator<<(std::ostream &os, Result res) {
  switch (res) {
    case Result::TTRUE:
      os << "true";
      break;

    case Result::FFALSE:
      os << "false";
      break;

    case Result::ABSTAIN:
      os << "abstain";
      break;
  }
  return os;
}

/// @brief Initializes the policy engine.
///
/// Must be called before using any other API functions.
/// Sets up internal state and prepares the engine for configuration and evaluation.
void init() {
  plcs_eval_ctx_init();
}
/// @brief Sets a numeric evaluation parameter.
///
/// @param evaluator The numeric evaluator key.
/// @param value The `long` value to associate with the evaluator.
void set_params(NumericEvaluator evaluator, long value) {
  plcs_eval_ctx_set_num_eval_param(static_cast<plcs_numeric_evaluators>(evaluator), value);
}

/// @brief Sets a numeric evaluation parameter.
///
/// @param evaluator The numeric evaluator key.
/// @param value The `unsigned long` value to associate with the evaluator.
void set_params(NumericEvaluator evaluator, unsigned long value) {
  plcs_eval_ctx_set_unum_eval_param(static_cast<plcs_numeric_evaluators>(evaluator), value);
}

/// @brief Sets a string evaluation parameter.
///
/// @param evaluator The string evaluator key.
/// @param value The `std::string_view` to associate with the evaluator.
void set_params(StringEvaluator evaluator, std::string_view value) {
  // TODO: Make sure value ends with `\0`.
  plcs_eval_ctx_set_str_eval_param(static_cast<plcs_string_evaluators>(evaluator), value.data());
}

/// @brief Registers a string evaluator function.
///
/// @param evaluator The string evaluator key.
/// @param func The evaluator function to register for the key.
void register_evaluator(StringEvaluator id, StringEvaluatorFunc cb) {
  string_evaluators_callback[static_cast<size_t>(id)] = cb;
  plcs_eval_ctx_register_str_evaluator(on_str_evaluator, static_cast<plcs_string_evaluators>(id));
}

/// @brief Registers a numeric evaluator function.
///
/// @param evaluator The numeric evaluator key.
/// @param func The evaluator function to register for the key.
void register_evaluator(NumericEvaluator id, NumericEvaluatorFunc cb) {
  numeric_evaluators_callback[static_cast<size_t>(id)] = cb;
  plcs_eval_ctx_register_num_evaluator(on_numeric_evaluator, static_cast<plcs_numeric_evaluators>(id));
}

/// @brief Registers a policy action function.
///
/// @param action The action identifier.
/// @param func The function to invoke when this action is triggered.
void register_action(Action action, ActionFunc cb) {
  actions_callback[static_cast<size_t>(action)] = cb;
  plcs_eval_ctx_register_action(on_actions, static_cast<plcs_actions>(action));
}

/// @brief Evaluates a policy from a raw buffer.
///
/// @param buffer A vector of bytes representing the serialized policy.
/// @return An optional `Result`. Returns `std::nullopt` if evaluation fails or is invalid.
std::optional<Result> evaluate_buffer(const std::vector<uint8_t> &buffer) {
  auto res = plcs_evaluate_buffer((uint8_t *)buffer.data(), buffer.size());
  (void)res;
  return std::nullopt;
}

/// @brief Evaluates a policy from a file.
///
/// @param path The filesystem path to the policy file.
/// @return An optional `Result`. Returns `std::nullopt` if file read or evaluation fails.
#ifdef _MSC_VER
// MSVC is broken ffs.
std::optional<Result> evaluate_buffer_from_file(const std::filesystem::path &filepath) {
  if (!std::filesystem::exists(filepath))
    return std::nullopt;

  FILE *file = NULL;
  fopen_s(&file, filepath.string().c_str(), "rb");
  if (!file) {
    return std::nullopt;
  }

  fseek(file, 0, SEEK_END);
  const auto file_size = ftell(file);
  fseek(file, 0, SEEK_SET);

  std::vector<uint8_t> buffer;
  buffer.reserve(file_size);

  const auto read_size = fread(buffer.data(), 1, file_size, file);
  fclose(file);

  if (read_size != file_size) {
    return std::nullopt;
  }

  return evaluate_buffer(buffer);
}
#else
std::optional<Result> evaluate_buffer_from_file(const std::filesystem::path &filepath) {
  if (!std::filesystem::exists(filepath))
    return std::nullopt;

  std::ifstream f(filepath, std::ios::binary);
  if (!f.is_open()) {
    return std::nullopt;
  }

  std::vector<uint8_t> buffer(std::istreambuf_iterator<char>(f), {});
  return evaluate_buffer(buffer);
}
#endif
}  // namespace datadog::wls
