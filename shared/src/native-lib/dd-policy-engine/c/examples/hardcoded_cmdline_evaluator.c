/**
 * @file hardcoded_cmdline_evaluator.c
 * @brief Example of a hardcoded command-line policy evaluator.
 *
 * Demonstrates basic usage of policy evaluation for command-line arguments.
 */

#include "file_oper.h"

#include <dd/policies/action.h>
#include <dd/policies/error_codes.h>
#include <dd/policies/eval_ctx.h>
#include <dd/policies/evaluator_default.h>
#include <dd/policies/evaluator_types.h>
#include <dd/policies/policies.h>
#include <stdio.h>
#include <stdlib.h>

#define DEFAULT_ALLOW_INJECTION EVAL_RESULT_TRUE

plcs_evaluation_result allow_injection = DEFAULT_ALLOW_INJECTION;  // Default to allowing injection

void set_allow_injection(plcs_evaluation_result res) {
  printf(
      "allow_injection: '%s' -> '%s'\n", plcs_evaluation_result_to_string(allow_injection),
      plcs_evaluation_result_to_string(res)
  );
  allow_injection = res;
}

// Demo action handler for INJECT_DENY action
plcs_errors ACTION_INJECT_DENY(
    plcs_evaluation_result res,
    char *values[],
    size_t value_len,
    const char *description,
    int action_id
) {
  printf(
      "Action: DENY [%s][%s][%s]\n", description, (char *)plcs_actions_to_string(action_id),
      plcs_evaluation_result_to_string(res)
  );

  for (size_t ix = 0; ix < value_len; ++ix) {
    printf("  -> Value[%lu]: '%s'\n", ix, values[ix]);
  }

  switch (res) {
    case EVAL_RESULT_TRUE:
      set_allow_injection(EVAL_RESULT_FALSE);
      break;
    case EVAL_RESULT_FALSE:
      // do nothing, it means we didn't really match
      break;
    case EVAL_RESULT_ABSTAIN:
      // do nothing!
      break;

    case EVAL_RESULT__COUNT:
      break;
  }
  return DD_ESUCCESS;
}

plcs_errors ACTION_INJECT_ALLOW(
    plcs_evaluation_result res,
    char *values[],
    size_t value_len,
    const char *description,
    int action_id
) {
  printf(
      "Action: ALLOW [%s][%s][%s]\n", description, plcs_actions_to_string(action_id),
      plcs_evaluation_result_to_string(res)
  );

  for (size_t ix = 0; ix < value_len; ++ix) {
    printf("  -> Value[%lu]: '%s'\n", ix, values[ix]);
  }

  switch (res) {
    case EVAL_RESULT_TRUE:
      set_allow_injection(EVAL_RESULT_TRUE);
      break;
    case EVAL_RESULT_FALSE:
      // do nothing, it means we didn't really match
      break;
    case EVAL_RESULT_ABSTAIN:
      // do whatever the default says
      set_allow_injection(DEFAULT_ALLOW_INJECTION);
      break;

    case EVAL_RESULT__COUNT:
      break;
  }

  return DD_ESUCCESS;
}

// Demo evaluator for runtime language detection
plcs_evaluation_result EVALUATOR_RUNTIME_LANGUAGE(
    const char *policy,
    const plcs_string_comparator cmp,
    const char *ctx,
    const char *description,
    plcs_string_evaluators eval_id
) {
  plcs_evaluation_result res = plcs_default_string_evaluator(policy, cmp, ctx, description, eval_id);
  printf(
      "Debug Evaluator: [%s](%s [%s] %s = %s)[%s]\n", plcs_string_evaluators_to_string(eval_id), policy,
      plcs_string_comparator_to_string(cmp), ctx, plcs_evaluation_result_to_string(res), description
  );

  return res;
}

void init() {
  // Initialize policy evaluation context
  int res = plcs_eval_ctx_init();
  if (res != DD_ESUCCESS) {
    plcs_eval_ctx_reset();
  }
  set_allow_injection(DEFAULT_ALLOW_INJECTION);
  printf("Evaluation context initialized\n");

  // Register action handlers
  plcs_eval_ctx_register_action(ACTION_INJECT_DENY, INJECT_DENY);
  plcs_eval_ctx_register_action(ACTION_INJECT_ALLOW, INJECT_ALLOW);
}

plcs_errors test_java_classes(const char *classname, uint8_t *buffer, size_t buffer_size) {
  init();
  // Register evaluators and set parameters
  REGISTER_STR_EVAL_PARAM(STR_EVAL_RUNTIME_LANGUAGE, EVALUATOR_RUNTIME_LANGUAGE, "jvm");
  REGISTER_STR_EVAL_PARAM(STR_EVAL_RUNTIME_ENTRY_POINT_CLASS, EVALUATOR_RUNTIME_LANGUAGE, classname);

  // Evaluate policy
  printf("test_java_classes ('%s')\n\tEvaluating policies...\n", classname);
  return plcs_evaluate_buffer(buffer, buffer_size);
}

plcs_errors test_python_entry(const char *entry_file, uint8_t *buffer, size_t buffer_size) {
  init();
  // Register evaluators and set parameters
  REGISTER_STR_EVAL_PARAM(STR_EVAL_RUNTIME_LANGUAGE, EVALUATOR_RUNTIME_LANGUAGE, "python");
  REGISTER_STR_EVAL_PARAM(STR_EVAL_RUNTIME_ENTRY_POINT_CLASS, EVALUATOR_RUNTIME_LANGUAGE, entry_file);

  // Evaluate policy
  printf("test_python_entry ('%s')\n\tEvaluating policies...\n", entry_file);
  return plcs_evaluate_buffer(buffer, buffer_size);
}

plcs_errors test_any_bin(const char *filepath, const char *runtime, uint8_t *buffer, size_t buffer_size) {
  init();
  // Register evaluators and set parameters
  REGISTER_STR_EVAL_PARAM(STR_EVAL_RUNTIME_LANGUAGE, EVALUATOR_RUNTIME_LANGUAGE, runtime);
  REGISTER_STR_EVAL_PARAM(STR_EVAL_RUNTIME_ENTRY_POINT_CLASS, EVALUATOR_RUNTIME_LANGUAGE, filepath);

  // Evaluate policy
  printf("test_any_bin ('%s')\n\tEvaluating policies...\n", filepath);
  return plcs_evaluate_buffer(buffer, buffer_size);
}

void print_test_end() {
  printf(
      "Allow injection: %s\n", allow_injection == EVAL_RESULT_TRUE    ? "true"
                               : allow_injection == EVAL_RESULT_FALSE ? "false"
                                                                      : "dont-care"
  );
  printf("Evaluation completed\n");
  printf("--------------------------------------------------\n");
}

void tests_java_classes(uint8_t *buffer, size_t buffer_size) {
  const char *test_files[] = {
      "org.apache.hadoop.hbase", "org.apache.hadoop.hbase.something.someClass", "org.apache.hadoop", NULL
  };

  for (size_t i = 0; test_files[i] != NULL; i++) {
    printf("Testing binary: %s\n", test_files[i]);
    plcs_errors res = test_java_classes(test_files[i], buffer, buffer_size);
    printf("Result for '%s': %s\n", test_files[i], res == DD_ESUCCESS ? "Success" : "Failure");
    print_test_end();
  }
}

void tests_python_entry(uint8_t *buffer, size_t buffer_size) {
  const char *test_files[] = {"/bin/ls", "/usr/bin/python3", "/usr/bin/gunicorn", "/usr/bin/java", NULL};

  for (size_t i = 0; test_files[i] != NULL; i++) {
    printf("Testing binary: %s\n", test_files[i]);
    plcs_errors res = test_python_entry(test_files[i], buffer, buffer_size);
    printf("Result for '%s': %s\n", test_files[i], res == DD_ESUCCESS ? "Success" : "Failure");
    print_test_end();
  }
}

void tests_any_bin(uint8_t *buffer, size_t buffer_size) {
  struct {
    const char *path;
    const char *runtime;
  } test_cases[] = {
      {"/bin/ls", "cpp"},
      {"/sbin/some_java_entry_file", "jvm"},  // although we're not doing java entryfile
      {"/usr/bin/some_unlisted_python_entry_file", "python"},
      {"/usr/bin/should_be_denied_ruby_entry", "ruby"},
      {NULL, NULL}
  };

  for (int i = 0; test_cases[i].path != NULL; i++) {
    printf("Testing binary: %s (runtime: %s)\n", test_cases[i].path, test_cases[i].runtime);
    plcs_errors res = test_any_bin(test_cases[i].path, test_cases[i].runtime, buffer, buffer_size);
    printf(
        "Result for '%s'/'%s': %s\n", test_cases[i].path, test_cases[i].runtime,
        res == DD_ESUCCESS ? "Success" : "Failure"
    );
    print_test_end();
  }
}

int main(int argc, char *argv[]) {
  if (argc < 2) {
    fprintf(stderr, "Usage: %s <path_to_policy_file>\n", argv[0]);
    return EXIT_FAILURE;
  }

  // Read policy file
  size_t buffer_size;
  uint8_t *buffer = read_file_contents(argv[1], &buffer_size);
  if (!buffer) {
    return EXIT_FAILURE;
  }
  printf("Successfully read %zu bytes from '%s'\n", buffer_size, argv[1]);

  printf("Starting policy evaluation...\n");

  tests_any_bin(buffer, buffer_size);
  tests_java_classes(buffer, buffer_size);
  tests_python_entry(buffer, buffer_size);

  free(buffer);

  printf("Policy evaluation completed successfully\n");
  return EXIT_SUCCESS;
}
