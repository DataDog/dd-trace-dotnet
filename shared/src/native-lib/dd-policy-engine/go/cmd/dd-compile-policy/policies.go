package main

import (
	"fmt"

	"github.com/DataDog/dd-policy-engine/go/schema/dd/wls"

	"github.com/DataDog/dd-policy-engine/go/schema"

	flatbuffers "github.com/google/flatbuffers/go"
)

// In this file we define the mapping from the JSON schema to the FlatBuffers schema.
// It requires generation of both the FlatBuffers schema and the corresponding Go types to
// unmarshal JSON in.

func PoliciesFbsFromSchema(fbBuilder *flatbuffers.Builder, jsonPolicies wls.PoliciesJSON) (flatbuffers.UOffsetT, error) {
	policiesFbs := []flatbuffers.UOffsetT{}
	for _, policy := range jsonPolicies.Policies {
		policyFbs, err := PolicyFbsFromSchema(fbBuilder, policy)
		if err != nil {
			return 0, err
		}
		policiesFbs = append(policiesFbs, policyFbs)
	}
	return schema.PoliciesCreate(
		fbBuilder,
		policiesFbs,
	), nil
}

func PolicyFbsFromSchema(fbBuilder *flatbuffers.Builder, jsonPolicy wls.PolicyJSON) (flatbuffers.UOffsetT, error) {
	nodeFbs, err := NodeFbsFromSchema(fbBuilder, jsonPolicy.Rules)
	if err != nil {
		return 0, err
	}
	return schema.PolicyCreate(
		fbBuilder,
		jsonPolicy.Description,
		nodeFbs,
		ActionsFbsFromSchema(fbBuilder, jsonPolicy.Actions),
	), nil
}

func NodeFbsFromSchema(fbBuilder *flatbuffers.Builder, jsonNode wls.NodeTypeWrapperJSON) (flatbuffers.UOffsetT, error) {
	node, err := jsonNode.DecodeNode()
	if err != nil {
		return 0, err
	}

	var nodeFbs flatbuffers.UOffsetT
	switch jsonNode.NodeType {
	case wls.NodeTypeEvaluatorNode:
		evaluatorNode := node.(wls.EvaluatorNodeJSON)
		nodeFbs, err = EvaluatorNodeFbsFromSchema(fbBuilder, evaluatorNode)
		if err != nil {
			return 0, err
		}
	case wls.NodeTypeCompositeNode:
		compositeNode := node.(wls.CompositeNodeJSON)
		nodeFbs, err = CompositeNodeFbsFromSchema(fbBuilder, compositeNode)
		if err != nil {
			return 0, err
		}
	case wls.NodeTypeNONE:
		return 0, nil // Not sure what to do here tbh
	default:
		return 0, fmt.Errorf("unknown node type: %v", jsonNode.NodeType)
	}
	return schema.NodeTypeWrapperCreate(
		fbBuilder,
		nodeFbs,
		jsonNode.NodeType,
	), nil
}

func CompositeNodeFbsFromSchema(fbBuilder *flatbuffers.Builder, jsonCompositeNode wls.CompositeNodeJSON) (flatbuffers.UOffsetT, error) {
	nodesFbs := []flatbuffers.UOffsetT{}
	for _, child := range jsonCompositeNode.Children {
		childFbs, err := NodeFbsFromSchema(fbBuilder, child)
		if err != nil {
			return 0, err
		}
		nodesFbs = append(nodesFbs, childFbs)
	}
	return schema.CompositeNodeCreate(
		fbBuilder,
		jsonCompositeNode.Op,
		jsonCompositeNode.Description,
		nodesFbs,
	), nil
}

func EvaluatorNodeFbsFromSchema(fbBuilder *flatbuffers.Builder, jsonEvaluatorNode wls.EvaluatorNodeJSON) (flatbuffers.UOffsetT, error) {
	evaluatorNode, err := jsonEvaluatorNode.DecodeEval()
	if err != nil {
		return 0, err
	}

	var evaluatorNodeFbs flatbuffers.UOffsetT
	switch jsonEvaluatorNode.EvalType {
	case wls.EvaluatorTypeStrEvaluator:
		strEvaluator := evaluatorNode.(wls.StrEvaluatorJSON)
		evaluatorNodeFbs = StrEvaluatorFbsFromSchema(fbBuilder, strEvaluator)
	case wls.EvaluatorTypeNumEvaluator:
		numEvaluator := evaluatorNode.(wls.NumEvaluatorJSON)
		evaluatorNodeFbs = NumEvaluatorFbsFromSchema(fbBuilder, numEvaluator)
	case wls.EvaluatorTypeUNumEvaluator:
		uNumEvaluator := evaluatorNode.(wls.UNumEvaluatorJSON)
		evaluatorNodeFbs = UNumEvaluatorFbsFromSchema(fbBuilder, uNumEvaluator)
	case wls.EvaluatorTypeNONE:
		// Not sure what to do here tbh
		evaluatorNodeFbs = 0
	default:
		return 0, fmt.Errorf("unknown evaluator type: %v", jsonEvaluatorNode.EvalType)
	}

	return schema.EvaluatorNodeCreate(
		fbBuilder,
		jsonEvaluatorNode.EvalType,
		jsonEvaluatorNode.Description,
		evaluatorNodeFbs,
	), nil
}

func StrEvaluatorFbsFromSchema(fbBuilder *flatbuffers.Builder, jsonStrEvaluator wls.StrEvaluatorJSON) flatbuffers.UOffsetT {
	return schema.StrEvaluatorCreate(
		fbBuilder,
		jsonStrEvaluator.Id,
		jsonStrEvaluator.Value,
		jsonStrEvaluator.Cmp,
	)
}

func NumEvaluatorFbsFromSchema(fbBuilder *flatbuffers.Builder, jsonNumEvaluator wls.NumEvaluatorJSON) flatbuffers.UOffsetT {
	return schema.NumEvaluatorCreate(
		fbBuilder,
		jsonNumEvaluator.Id,
		jsonNumEvaluator.Value,
		jsonNumEvaluator.Cmp,
	)
}

func UNumEvaluatorFbsFromSchema(fbBuilder *flatbuffers.Builder, jsonUNumEvaluator wls.UNumEvaluatorJSON) flatbuffers.UOffsetT {
	return schema.UNumEvaluatorCreate(
		fbBuilder,
		jsonUNumEvaluator.Id,
		jsonUNumEvaluator.Value,
		jsonUNumEvaluator.Cmp,
	)
}

func ActionsFbsFromSchema(fbBuilder *flatbuffers.Builder, jsonActions []wls.ActionJSON) []flatbuffers.UOffsetT {
	actions := []flatbuffers.UOffsetT{}
	for _, action := range jsonActions {
		actions = append(actions, ActionFbsFromSchema(fbBuilder, action))
	}
	return actions
}

func ActionFbsFromSchema(fbBuilder *flatbuffers.Builder, jsonAction wls.ActionJSON) flatbuffers.UOffsetT {
	return schema.ActionCreate(
		fbBuilder,
		jsonAction.Action,
		jsonAction.Description,
		jsonAction.Values,
	)
}
