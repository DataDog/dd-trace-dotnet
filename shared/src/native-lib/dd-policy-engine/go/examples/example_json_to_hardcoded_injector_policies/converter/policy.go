package converter

import (
	"fmt"

	"github.com/DataDog/dd-policy-engine/go/schema"
	"github.com/DataDog/dd-policy-engine/go/schema/dd/wls"

	flatbuffers "github.com/google/flatbuffers/go"
)

type JSONPolicy struct {
	Skip        bool            `json:"skip"`
	Description string          `json:"description"`
	Runtime     string          `json:"runtime"`
	Conditions  []JSONCondition `json:"conditions"`
}

func AddRuntimeCondition(runtime string) bool {
	switch runtime {
	case "all":
		return false

	case "python", "java", "nodejs", "dotnet", "ruby", "go", "php":
		return true
	}

	return false
}

func GetActionId(skip bool) wls.ActionId {
	if skip {
		return wls.ActionIdINJECT_DENY
	}
	return wls.ActionIdINJECT_ALLOW
}

func appendRuntimeRule(builder *flatbuffers.Builder, node flatbuffers.UOffsetT, runtime string, description string) (flatbuffers.UOffsetT, error) {
	fmt.Printf("Adding runtime rule!\n")

	str_evaluator := schema.StrEvaluatorCreate(builder, wls.StringEvaluatorsRUNTIME_LANGUAGE, runtime, wls.CmpTypeSTRCMP_EXACT)

	str_evaluator_node := schema.EvaluatorNodeCreate(builder, wls.EvaluatorTypeStrEvaluator, "Runtime matching", str_evaluator)

	language_node := schema.NodeTypeWrapperCreate(builder, str_evaluator_node, wls.NodeTypeEvaluatorNode)

	return schema.NodeTypeWrapperCreate(builder, schema.CompositeNodeCreate(builder, wls.BoolOperationBOOL_AND, description, []flatbuffers.UOffsetT{language_node, node}), wls.NodeTypeCompositeNode), nil // Example for runtime condition, adjust as needed
}

func (p JSONPolicy) ConvertToWLS(builder *flatbuffers.Builder) (flatbuffers.UOffsetT, error) {
	nodesCount := len(p.Conditions)
	var conditionsNode, root flatbuffers.UOffsetT
	if nodesCount == 0 {
		return 0, fmt.Errorf("No children to add to policy")
	}

	if nodesCount == 1 {
		conditionsNode, _ = p.Conditions[0].ConvertToWLS(builder)
	}

	if nodesCount > 1 {
		var nodes []flatbuffers.UOffsetT

		for _, condition := range p.Conditions {
			node, err := condition.ConvertToWLS(builder)
			if err != nil {
				continue
			}
			nodes = append(nodes, node)
		}
		conditionsNode = schema.NodeTypeWrapperCreate(builder, schema.CompositeNodeCreate(builder, wls.BoolOperationBOOL_OR, p.Description, nodes), wls.NodeTypeCompositeNode)
	}

	if AddRuntimeCondition(p.Runtime) {
		root, _ = appendRuntimeRule(builder, conditionsNode, p.Runtime, p.Description)
	} else {
		root = conditionsNode
	}

	action := schema.ActionCreate(builder, GetActionId(p.Skip), p.Description, []string{p.Runtime})

	return schema.PolicyCreate(builder, p.Description, root, []flatbuffers.UOffsetT{action}), nil
}
