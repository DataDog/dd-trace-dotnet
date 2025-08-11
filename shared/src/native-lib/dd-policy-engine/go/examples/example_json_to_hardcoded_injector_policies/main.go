package main

import (
	"encoding/json"
	"fmt"
	"io"
	"log"
	"os"
	"strings"

	"github.com/DataDog/dd-policy-engine/go/examples/example_json_to_hardcoded_injector_policies/converter"

	flatbuffers "github.com/google/flatbuffers/go"
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

func finalizePolicies(builder *flatbuffers.Builder, policies flatbuffers.UOffsetT, fileName string, outDir string) {

	builder.Finish(policies)
	buffer := builder.FinishedBytes()

	header := generateCHeader("hardcoded_policies", buffer)

	err := os.MkdirAll(outDir, 0755)
	if err != nil {
		fmt.Printf("Error creating directory: %v\n", err)
		return
	}

	os.WriteFile(outDir+fileName+".h", []byte(header), 0644)

	writeBufferToFile(buffer, fileName+".bin")
}

func main() {
	// Get filename from command line arguments or use default
	filename := "skips.json"
	if len(os.Args) > 1 {
		filename = os.Args[1]
	}

	file, err := os.Open(filename)
	if err != nil {
		log.Fatalf("Failed to open skips.json: %v", err)
	}
	defer file.Close()

	bytes, err := io.ReadAll(file)
	if err != nil {
		log.Fatalf("Failed to read skips.json: %v", err)
	}

	var policies converter.JSONPolicies
	if err := json.Unmarshal(bytes, &policies); err != nil {
		log.Fatalf("Failed to parse JSON: %v", err)
	}

	for idx, value := range policies.Policies {
		fmt.Printf("Index: %d, Value: %v\n", idx, value)
	}

	builder := flatbuffers.NewBuilder(1024)

	offset, err := policies.ConvertToWLS(builder)
	if err != nil {
		log.Fatalf("Failed to convert policies to WLS: %v", err)
	}
	finalizePolicies(builder, offset, "hardcoded", "out/")

}
