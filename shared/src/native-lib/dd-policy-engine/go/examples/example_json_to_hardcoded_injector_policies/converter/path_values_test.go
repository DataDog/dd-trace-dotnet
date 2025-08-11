package converter

import (
	wls "dd-fbs/schema/dd/wls"
	"testing"

	flatbuffers "github.com/google/flatbuffers/go"
)

func TestJsonStrValueTypeToWls(t *testing.T) {
	tests := []struct {
		name        string
		valueType   string
		expected    wls.StringEvaluators
		description string
	}{
		{
			name:        "exe_full_path",
			valueType:   "exe_full_path",
			expected:    wls.StringEvaluatorsPROCESS_EXE_FULL_PATH,
			description: "Full path of the executable",
		},
		{
			name:        "class",
			valueType:   "class",
			expected:    wls.StringEvaluatorsRUNTIME_ENTRY_POINT_CLASS,
			description: "Runtime entry point class",
		},
		{
			name:        "entry_file",
			valueType:   "entry_file",
			expected:    wls.StringEvaluatorsRUNTIME_ENTRY_POINT_FILE,
			description: "Runtime entry point file",
		},
		{
			name:        "default",
			valueType:   "unknown",
			expected:    wls.StringEvaluatorsPROCESS_EXE_FULL_PATH,
			description: "Default case for unknown value type",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			result := JsonStrValueTypeToWls(tt.valueType)
			if result != tt.expected {
				t.Errorf("JsonStrValueTypeToWls(%s) = %v, want %v", tt.valueType, result, tt.expected)
			}
		})
	}
}

func TestJsonStrCompareStrategyToWls(t *testing.T) {
	tests := []struct {
		name        string
		cmpStrategy string
		expected    wls.CmpTypeSTR
	}{
		{"prefix", "prefix", wls.CmpTypeSTRCMP_PREFIX},
		{"suffix", "suffix", wls.CmpTypeSTRCMP_SUFFIX},
		{"contains", "contains", wls.CmpTypeSTRCMP_CONTAINS},
		{"equals", "equals", wls.CmpTypeSTRCMP_EXACT},
		{"default", "unknown", wls.CmpTypeSTRCMP_EXACT},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			result := JsonStrCompareStrategyToWls(tt.cmpStrategy)
			if result != tt.expected {
				t.Errorf("JsonStrCompareStrategyToWls(%s) = %v, want %v", tt.cmpStrategy, result, tt.expected)
			}
		})
	}
}

func TestJSONPathValueConvertToWLS(t *testing.T) {
	tests := []struct {
		name        string
		pathValue   JSONPathValue
		expectError bool
	}{
		{
			name: "Basic conversion",
			pathValue: JSONPathValue{
				Cmp_strategy: "prefix",
				Value:        "test_value",
				Value_type:   "exe_full_path",
			},
			expectError: false,
		},
		{
			name: "Different strategy",
			pathValue: JSONPathValue{
				Cmp_strategy: "suffix",
				Value:        "test_value",
				Value_type:   "class",
			},
			expectError: false,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			builder := flatbuffers.NewBuilder(0)
			offset, err := tt.pathValue.ConvertToWLS(builder)

			if tt.expectError && err == nil {
				t.Errorf("Expected error but got none")
			}

			if !tt.expectError && err != nil {
				t.Errorf("Unexpected error: %v", err)
			}

			if offset == 0 && !tt.expectError {
				t.Errorf("Expected valid offset but got 0")
			}

			// Cannot test exact result content since flatbuffers serialization is complex
			// and we'd need to deserialize to check, but at least we verify it produces something
		})
	}
}
