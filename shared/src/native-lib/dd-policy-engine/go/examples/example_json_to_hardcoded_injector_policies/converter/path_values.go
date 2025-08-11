package converter

import (
	"github.com/DataDog/dd-policy-engine/go/schema"
	"github.com/DataDog/dd-policy-engine/go/schema/dd/wls"

	flatbuffers "github.com/google/flatbuffers/go"
)

type CompareStrategy int

type JSONPathValue struct {
	Cmp_strategy string `json:"cmp_strategy"`
	Value        string `json:"value"`
	Value_type   string `json:"value_type"` // Value_type should be converted to wls.StringEvaluators
	Description  string `json:"description,omitempty"`
}

func strOrEmpty(s *string) string {
	if s != nil {
		return *s
	}
	return ""
}

func JsonStrValueTypeToWls(value_type string) wls.StringEvaluators {
	switch value_type {
	case "exe_full_path":
		return wls.StringEvaluatorsPROCESS_EXE_FULL_PATH
	case "class":
		return wls.StringEvaluatorsRUNTIME_ENTRY_POINT_CLASS // Example, adjust as needed
	case "entry_file":
		return wls.StringEvaluatorsRUNTIME_ENTRY_POINT_FILE // Example, adjust as needed
	default:
		return wls.StringEvaluatorsPROCESS_EXE_FULL_PATH // Default case, can be adjusted as needed
	}
}

func JsonStrCompareStrategyToWls(cmp_strategy string) wls.CmpTypeSTR {
	switch cmp_strategy {
	case "prefix":
		return wls.CmpTypeSTRCMP_PREFIX
	case "suffix":
		return wls.CmpTypeSTRCMP_SUFFIX
	case "contains":
		return wls.CmpTypeSTRCMP_CONTAINS
	case "equals":
		return wls.CmpTypeSTRCMP_EXACT
	default:
		return wls.CmpTypeSTRCMP_EXACT // Default case, can be adjusted as needed
	}
}

func (v JSONPathValue) ConvertToWLS(builder *flatbuffers.Builder) (flatbuffers.UOffsetT, error) {

	str_evaluator := schema.StrEvaluatorCreate(builder, JsonStrValueTypeToWls(v.Value_type), v.Value, JsonStrCompareStrategyToWls(v.Cmp_strategy))

	str_evaluator_node := schema.EvaluatorNodeCreate(builder, wls.EvaluatorTypeStrEvaluator, strOrEmpty(&v.Description), str_evaluator)

	return schema.NodeTypeWrapperCreate(builder, str_evaluator_node, wls.NodeTypeEvaluatorNode), nil

}
