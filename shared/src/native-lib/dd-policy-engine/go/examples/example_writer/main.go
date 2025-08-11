// flatc -g  -o ../go/schema *.fbs

package main

import (
	"fmt"
	"log"
	"os"

	flatbuffers "github.com/google/flatbuffers/go"
	// Import the generated FlatBuffers schema packages

	"github.com/DataDog/dd-policy-engine/go/schema"
	"github.com/DataDog/dd-policy-engine/go/schema/dd/wls"
)

func writeBufferToFile(buffer []byte, fileName string) {
	outDir := "out/"

	err := os.MkdirAll(outDir, 0755)
	if err != nil {
		fmt.Printf("Error creating directory: %v\n", err)
		return
	}

	err = os.WriteFile(outDir+fileName, buffer, 0644)
	if err != nil {
		log.Fatalf("Failed to write buffer to file: %v", err)
	}
	fmt.Printf("Wrote %d bytes to: %s\n\n", len(buffer), fileName)
}

func createStrEvaluatorNode(builder *flatbuffers.Builder, evaluatorId wls.StringEvaluators, value string, cmpp wls.CmpTypeSTR, description string) flatbuffers.UOffsetT {
	evaluator := schema.StrEvaluatorCreate(builder, evaluatorId, value, cmpp)
	node := schema.EvaluatorNodeCreate(builder, wls.EvaluatorTypeStrEvaluator, description, evaluator)
	return schema.NodeTypeWrapperCreate(builder, node, wls.NodeTypeEvaluatorNode)
}

func createDenyByRuntimePolicy(builder *flatbuffers.Builder, runtime string) flatbuffers.UOffsetT {
	// Start by creating the leaf evaluator (StrEvaluator for runtime language check)
	node := createStrEvaluatorNode(builder, wls.StringEvaluatorsRUNTIME_LANGUAGE, runtime, wls.CmpTypeSTRCMP_EXACT, "Validate runtime is "+runtime)
	action := schema.ActionCreate(builder, wls.ActionIdINJECT_DENY, "Deny "+runtime+" runtime!", []string{"value_1", "value_2", "value_3"})
	return schema.PolicyCreate(builder, "DenyByRuntimePolicy("+runtime+")", node, []flatbuffers.UOffsetT{action})
}

func createRoot(builder *flatbuffers.Builder, oper wls.BoolOperation, description string, nodes []flatbuffers.UOffsetT) flatbuffers.UOffsetT {
	nodeRoot := schema.CompositeNodeCreate(builder, oper, description, nodes)
	return schema.NodeTypeWrapperCreate(builder, nodeRoot, wls.NodeTypeCompositeNode)
}

func createDenyByMultipleEvaluatorsPolicy(builder *flatbuffers.Builder, runtime string) flatbuffers.UOffsetT {

	// Start by creating the leaf evaluator (StrEvaluator for runtime language check)
	nodeRuntime := createStrEvaluatorNode(builder, wls.StringEvaluatorsRUNTIME_LANGUAGE, runtime, wls.CmpTypeSTRCMP_EXACT, "Validate runtime is "+runtime)

	nodeExePrefix := createStrEvaluatorNode(builder, wls.StringEvaluatorsPROCESS_EXE_FULL_PATH, "/some/stairway/to/heaven", wls.CmpTypeSTRCMP_PREFIX, "Validate runtime is "+runtime)

	nodeRoot := createRoot(builder, wls.BoolOperationBOOL_AND, "evaluate if runtime and exe prefix equal to something :)", []flatbuffers.UOffsetT{nodeRuntime, nodeExePrefix})

	action := schema.ActionCreate(builder, wls.ActionIdINJECT_DENY, "Deny "+runtime+" runtime!", []string{"value_1", "value_2", "value_3"})
	return schema.PolicyCreate(builder, "DenyByRuntimePolicy("+runtime+")", nodeRoot, []flatbuffers.UOffsetT{action})

}

func createFlatPolicy() string {
	fileName := "FlatPolicy.bin"
	builder := flatbuffers.NewBuilder(1024)
	javaPolicy := createDenyByRuntimePolicy(builder, "jvm")
	policies := schema.PoliciesCreate(builder, []flatbuffers.UOffsetT{javaPolicy})

	builder.Finish(policies)
	buffer := builder.FinishedBytes()
	writeBufferToFile(buffer, fileName)
	return fileName
}

func createMultipleFlatPolicies() string {
	fileName := "MultipleFlatPolicies.bin"
	builder := flatbuffers.NewBuilder(1024)
	javaPolicy := createDenyByRuntimePolicy(builder, "jvm")
	pythonPolicy := createDenyByRuntimePolicy(builder, "python")
	goPolicy := createDenyByRuntimePolicy(builder, "go")
	policies := schema.PoliciesCreate(builder, []flatbuffers.UOffsetT{javaPolicy, pythonPolicy, goPolicy})

	builder.Finish(policies)
	buffer := builder.FinishedBytes()
	writeBufferToFile(buffer, fileName)
	return fileName
}

func CreateDeepPolicy() string {
	fileName := "DeepPolicy.bin"
	builder := flatbuffers.NewBuilder(1024)
	deepPolicy := createDenyByMultipleEvaluatorsPolicy(builder, "ruby")
	policies := schema.PoliciesCreate(builder, []flatbuffers.UOffsetT{deepPolicy})

	builder.Finish(policies)
	buffer := builder.FinishedBytes()
	writeBufferToFile(buffer, fileName)
	return fileName
}

func createFlatPolicyNoEvaluators() string {
	fileName := "FlatPolicyNoEvaluators.bin"
	builder := flatbuffers.NewBuilder(1024)
	action := schema.ActionCreate(builder, wls.ActionIdINJECT_ALLOW, "These actions will execute without executing any evaluators", []string{"value_1", "value_2", "value_3", "value_4", "value_5"})
	policy := schema.PolicyCreate(builder, "AllowAllPolicy", 0, []flatbuffers.UOffsetT{action})
	policies := schema.PoliciesCreate(builder, []flatbuffers.UOffsetT{policy})

	builder.Finish(policies)
	buffer := builder.FinishedBytes()
	writeBufferToFile(buffer, fileName)
	return fileName
}

func createEmptyPolicies() string {
	fileName := "EmptyPolicies.bin"
	builder := flatbuffers.NewBuilder(1024)
	policy := schema.PolicyCreate(builder, "EmptyPolicy", 0, []flatbuffers.UOffsetT{})
	policies := schema.PoliciesCreate(builder, []flatbuffers.UOffsetT{policy})

	builder.Finish(policies)
	buffer := builder.FinishedBytes()
	writeBufferToFile(buffer, fileName)
	return fileName
}

func createFlatPolicyNoActions() string {
	fileName := "FlatPolicyNoActions.bin"
	builder := flatbuffers.NewBuilder(1024)
	node := createStrEvaluatorNode(builder, wls.StringEvaluatorsRUNTIME_LANGUAGE, "some runtime", wls.CmpTypeSTRCMP_EXACT, "Validate runtime is some runtime")
	policy := schema.PolicyCreate(builder, "PolicyWithoutActions", node, []flatbuffers.UOffsetT{})
	policies := schema.PoliciesCreate(builder, []flatbuffers.UOffsetT{policy})

	builder.Finish(policies)
	buffer := builder.FinishedBytes()
	writeBufferToFile(buffer, fileName)
	return fileName
}

func createZeropPolicies() string {
	fileName := "ZeropPolicies.bin"
	builder := flatbuffers.NewBuilder(1024)
	policy := schema.PolicyCreate(builder, "EmptyPolicy", 0, []flatbuffers.UOffsetT{})
	policies := schema.PoliciesCreate(builder, []flatbuffers.UOffsetT{policy})

	builder.Finish(policies)
	buffer := builder.FinishedBytes()
	writeBufferToFile(buffer, fileName)
	return fileName
}

func main() {
	// Create a new FlatBufferBuilder with an initial size
	createMultipleFlatPolicies()

	createFlatPolicy()

	CreateDeepPolicy()

	createFlatPolicyNoEvaluators()

	createEmptyPolicies()

	createFlatPolicyNoActions()

	createZeropPolicies()
}
