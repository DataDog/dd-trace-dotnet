// flatc -g  -o ../go/schema *.fbs

package main

import (
	"fmt"
	"log"
	"os"
	"strings"

	flatbuffers "github.com/google/flatbuffers/go"
	// Import the generated FlatBuffers schema packages

	"github.com/DataDog/dd-policy-engine/go/schema"
	"github.com/DataDog/dd-policy-engine/go/schema/dd/wls"
)

func writeBufferToFile(buffer []byte, fileName string) {
	err := os.WriteFile(fileName, buffer, 0644)
	if err != nil {
		log.Fatalf("Failed to write buffer to file: %v", err)
	}
	fmt.Printf("Wrote %d bytes to: %s\n\n", len(buffer), fileName)
}

func createStrEvaluatorNode(builder *flatbuffers.Builder, evaluatorId wls.StringEvaluators, value string, cmp wls.CmpTypeSTR, description string) flatbuffers.UOffsetT {
	evaluator := schema.StrEvaluatorCreate(builder, evaluatorId, value, cmp)
	node := schema.EvaluatorNodeCreate(builder, wls.EvaluatorTypeStrEvaluator, description, evaluator)
	return schema.NodeTypeWrapperCreate(builder, node, wls.NodeTypeEvaluatorNode)
}

func createDenyByRuntimePolicy(builder *flatbuffers.Builder, runtime string) flatbuffers.UOffsetT {
	// Start by creating the leaf evaluator (StrEvaluator for runtime language check)
	node := createStrEvaluatorNode(builder, wls.StringEvaluatorsRUNTIME_LANGUAGE, runtime, wls.CmpTypeSTRCMP_EXACT, "Validate runtime is "+runtime)
	action := schema.ActionCreate(builder, wls.ActionIdINJECT_DENY, "Deny "+runtime+" runtime!", []string{"value_1", "value_2", "value_3"})
	return schema.PolicyCreate(builder, "DenyByRuntimePolicy("+runtime+")", node, []flatbuffers.UOffsetT{action})
}

func generateCHeader(varName string, data []byte) string {
	var sb strings.Builder
	sb.WriteString("#pragma once\n\n")
	sb.WriteString("#include <stdint.h>\n\n")
	sb.WriteString(fmt.Sprintf("const uint8_t %s[] = {\n", varName))

	for i, b := range data {
		if i%12 == 0 {
			sb.WriteString("  ")
		}
		sb.WriteString(fmt.Sprintf("0x%02X", b))
		if i < len(data)-1 {
			sb.WriteString(", ")
		}
		if (i+1)%12 == 0 {
			sb.WriteString("\n")
		}
	}

	if len(data)%12 != 0 {
		sb.WriteString("\n")
	}

	sb.WriteString("};\n")
	sb.WriteString(fmt.Sprintf("const unsigned int %s_len = %d;\n", varName, len(data)))
	return sb.String()
}

func createFlatPolicies() {
	fileName := "buffer"
	outDir := "out/"
	builder := flatbuffers.NewBuilder(1024)
	javaPolicy := createDenyByRuntimePolicy(builder, "jvm")
	pythonPolicy := createDenyByRuntimePolicy(builder, "python")
	goPolicy := createDenyByRuntimePolicy(builder, "go")

	policies := schema.PoliciesCreate(builder, []flatbuffers.UOffsetT{javaPolicy, pythonPolicy, goPolicy})

	builder.Finish(policies)
	buffer := builder.FinishedBytes()
	header := generateCHeader("hardcoded_policies", buffer)

	err := os.MkdirAll(outDir, 0755)
	if err != nil {
		fmt.Printf("Error creating directory: %v\n", err)
		return
	}

	os.WriteFile(outDir+fileName+".h", []byte(header), 0644)

	writeBufferToFile(buffer, outDir+fileName+".bin")

}

func main() {
	// Create a new FlatBufferBuilder with an initial size
	createFlatPolicies()
}
