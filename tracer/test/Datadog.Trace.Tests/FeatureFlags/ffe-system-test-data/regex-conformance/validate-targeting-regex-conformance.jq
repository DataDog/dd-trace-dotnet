def nonempty_string:
  type == "string" and length > 0;

def nullable_boolean:
  type == "boolean" or . == null;

def nonempty_string_array:
  type == "array" and
  length > 0 and
  all(.[]; nonempty_string) and
  length == (unique | length);

def valid_semantics:
  type == "object" and
  (.acceptedSyntax | nonempty_string) and
  (.engines | nonempty_string_array) and
  (.matchMode | nonempty_string) and
  .nativeCompileFailureEvaluationResult == false and
  (.normalization | nonempty_string) and
  (.contract | nonempty_string) and
  (.expectedCompile | nonempty_string) and
  (.expectedMatch | nonempty_string) and
  (.engineExpectations | nonempty_string);

def valid_portable_syntax:
  type == "object" and
  (.accepted | nonempty_string_array) and
  (.rejected | nonempty_string_array) and
  ([.accepted[], .rejected[]] | length) ==
    ([.accepted[], .rejected[]] | unique | length);

def expected_engine_keys:
  ["go", "re2js", "rustRkyv", "rustRulesBased"];

def valid_engine_expectation:
  has("compile") and
  has("match") and
  (.compile | type) == "boolean" and
  (.match | nullable_boolean) and
  (if .compile then (.match | type) == "boolean" else .match == null end);

def valid_engine_expectations:
  (.engineExpectations | keys | sort) == expected_engine_keys and
  all(.engineExpectations[]; valid_engine_expectation) and
  ([.engineExpectations[].compile] | unique) as $compile_values |
  ([.engineExpectations[].match] | unique) as $match_values |
  (if ($compile_values | length) == 1
   then .expectedCompile == $compile_values[0]
   else .expectedCompile == null
   end) and
  (if ($match_values | length) == 1
   then .expectedMatch == $match_values[0]
   else .expectedMatch == null
   end);

def valid_case:
  . as $case |
  has("id") and
  has("description") and
  has("category") and
  has("contract") and
  has("rawPattern") and
  has("normalizedPattern") and
  has("expectedCompile") and
  has("input") and
  has("expectedMatch") and
  (.id | nonempty_string) and
  (.description | nonempty_string) and
  (.category | nonempty_string) and
  (.contract == "accepted" or .contract == "rejected") and
  (.id | startswith($case.contract + "-")) and
  (.rawPattern | type) == "string" and
  (.normalizedPattern | type) == "string" and
  (.expectedCompile | nullable_boolean) and
  (.input | type) == "string" and
  (.expectedMatch | nullable_boolean) and
  (if .contract == "accepted"
   then .expectedCompile == true and
        (.expectedMatch | type) == "boolean" and
        has("engineExpectations") == false
   else true
   end) and
  (if has("engineExpectations")
   then valid_engine_expectations
   else .expectedCompile != null and
        .expectedMatch != null and
        (if .expectedCompile == false then .expectedMatch == false else true end)
   end);

.schema == "datadog.ffe.targeting-regex-conformance/v1" and
.schemaVersion == 1 and
(.contractVersion | nonempty_string) and
(.semantics | valid_semantics) and
(.portableSyntax | valid_portable_syntax) and
(.cases | type) == "array" and
(.cases | length) > 0 and
([.cases[].id] | length) == ([.cases[].id] | unique | length) and
all(.cases[]; valid_case)
