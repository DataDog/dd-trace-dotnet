package schema

import (
	"github.com/DataDog/dd-policy-engine/go/schema/dd/wls"

	flatbuffers "github.com/google/flatbuffers/go"
)

func slicesToFBStrings(builder *flatbuffers.Builder, values []string) []flatbuffers.UOffsetT {
	fbValues := make([]flatbuffers.UOffsetT, len(values))
	for i, v := range values {
		fbValues[i] = builder.CreateString(v)
	}
	return fbValues
}

func ActionCreate(builder *flatbuffers.Builder, action wls.ActionId, description string, values []string) flatbuffers.UOffsetT {
	fbDescription := builder.CreateString(description)
	fbValues := builder.CreateVectorOfTables(slicesToFBStrings(builder, values))
	wls.ActionStart(builder)
	wls.ActionAddAction(builder, action)
	wls.ActionAddDescription(builder, fbDescription)
	wls.ActionAddValues(builder, fbValues)
	return wls.ActionEnd(builder)
}

func StrEvaluatorCreate(builder *flatbuffers.Builder, evaluator wls.StringEvaluators, value string, cmp wls.CmpTypeSTR) flatbuffers.UOffsetT {
	fbValue := builder.CreateString(value)

	wls.StrEvaluatorStart(builder)
	wls.StrEvaluatorAddCmp(builder, cmp)
	wls.StrEvaluatorAddId(builder, evaluator)
	wls.StrEvaluatorAddValue(builder, fbValue)
	return wls.StrEvaluatorEnd(builder)
}

func NumEvaluatorCreate(builder *flatbuffers.Builder, evaluator wls.NumericEvaluators, value int64, cmp wls.CmpTypeNUM) flatbuffers.UOffsetT {
	wls.NumEvaluatorStart(builder)
	wls.NumEvaluatorAddCmp(builder, cmp)
	wls.NumEvaluatorAddId(builder, evaluator)
	wls.NumEvaluatorAddValue(builder, value)
	return wls.NumEvaluatorEnd(builder)
}

func UNumEvaluatorCreate(builder *flatbuffers.Builder, evaluator wls.NumericEvaluators, value uint64, cmp wls.CmpTypeNUM) flatbuffers.UOffsetT {
	wls.UNumEvaluatorStart(builder)
	wls.UNumEvaluatorAddCmp(builder, cmp)
	wls.UNumEvaluatorAddId(builder, evaluator)
	wls.UNumEvaluatorAddValue(builder, value)
	return wls.UNumEvaluatorEnd(builder)
}

func EvaluatorNodeCreate(builder *flatbuffers.Builder, evaluatorType wls.EvaluatorType, description string, evalOffset flatbuffers.UOffsetT) flatbuffers.UOffsetT {
	fbDescription := builder.CreateString(description)

	wls.EvaluatorNodeStart(builder)
	wls.EvaluatorNodeAddEvalType(builder, evaluatorType)
	wls.EvaluatorNodeAddDescription(builder, fbDescription)
	wls.EvaluatorNodeAddEval(builder, evalOffset)

	return wls.EvaluatorNodeEnd(builder)
}

func CompositeNodeCreate(builder *flatbuffers.Builder, oper wls.BoolOperation, description string, nodes []flatbuffers.UOffsetT) flatbuffers.UOffsetT {
	fbDescription := builder.CreateString(description)
	childrenVector := builder.CreateVectorOfTables(nodes)
	wls.CompositeNodeStart(builder)
	wls.CompositeNodeAddDescription(builder, fbDescription)
	wls.CompositeNodeAddOp(builder, oper)
	wls.CompositeNodeAddChildren(builder, childrenVector)
	return wls.CompositeNodeEnd(builder)
}

func NodeTypeWrapperCreate(builder *flatbuffers.Builder, nodeOffset flatbuffers.UOffsetT, nodeType wls.NodeType) flatbuffers.UOffsetT {
	wls.NodeTypeWrapperStart(builder)
	wls.NodeTypeWrapperAddNode(builder, nodeOffset)
	wls.NodeTypeWrapperAddNodeType(builder, nodeType)
	return wls.NodeTypeWrapperEnd(builder)
}

func PolicyCreate(builder *flatbuffers.Builder, description string, rulesRoot flatbuffers.UOffsetT, actions []flatbuffers.UOffsetT) flatbuffers.UOffsetT {
	fbDescription := builder.CreateString(description)
	actionsVector := builder.CreateVectorOfTables(actions)

	wls.PolicyStart(builder)
	wls.PolicyAddDescription(builder, fbDescription)
	wls.PolicyAddRules(builder, rulesRoot)
	wls.PolicyAddActions(builder, actionsVector)

	return wls.PolicyEnd(builder)
}

func PoliciesCreate(builder *flatbuffers.Builder, policies []flatbuffers.UOffsetT) flatbuffers.UOffsetT {
	policiesVector := builder.CreateVectorOfTables(policies)
	wls.PoliciesStart(builder)
	wls.PoliciesAddPolicies(builder, policiesVector)
	return wls.PoliciesEnd(builder)
}
