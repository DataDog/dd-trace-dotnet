package main

import (
	"bufio"
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"strings"
)

// Base FlatBuffers to Go type map
var typeMap = map[string]string{
	"string": "string",
	"int":    "int",
	"float":  "float32",
	"bool":   "bool",
	"ulong":  "uint64",
	"long":   "int64",
}

// Enums and Unions
var enumTypes = make(map[string]bool)
var enumList []string // preserve order for output

type UnionDef struct {
	Name    string
	Members []string
}

var unionDefs = make(map[string]UnionDef)

// Convert FlatBuffers type to Go type (with JSON suffix unless built-in or enum)
func convertType(fbType string) string {
	isArray := strings.HasPrefix(fbType, "[") && strings.HasSuffix(fbType, "]")
	baseType := strings.Trim(fbType, "[]")

	// Base types
	if goType, ok := typeMap[baseType]; ok {
		if isArray {
			return "[]" + goType
		}
		return goType
	}

	// Enum — return original enum type (no JSON suffix)
	if enumTypes[baseType] {
		if isArray {
			return "[]" + baseType
		}
		return baseType
	}

	// Union - note: unions do NOT get JSON suffix structs, handled separately in parent
	if _, ok := unionDefs[baseType]; ok {
		if isArray {
			panic("array of unions not supported")
		}
		return baseType
	}

	// Table or Struct — add JSON suffix
	goType := baseType + "JSON"
	if isArray {
		return "[]" + goType
	}
	return goType
}

// First pass: detect enums and unions
func findEnumsAndUnions(path string) error {
	file, err := os.Open(path)
	if err != nil {
		return err
	}
	defer file.Close()

	reEnum := regexp.MustCompile(`^enum\s+(\w+)\s*:`)
	reUnionStart := regexp.MustCompile(`^union\s+(\w+)\s*\{?`)
	reUnionEnd := regexp.MustCompile(`\}`)

	var (
		inUnion     bool
		unionName   string
		unionFields []string
	)

	scanner := bufio.NewScanner(file)
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())

		// Match enums
		if !inUnion {
			if m := reEnum.FindStringSubmatch(line); m != nil {
				enumName := m[1]
				if !enumTypes[enumName] {
					enumTypes[enumName] = true
					enumList = append(enumList, enumName)
				}
				continue
			}
		}

		// Start of union
		if !inUnion {
			if m := reUnionStart.FindStringSubmatch(line); m != nil {
				inUnion = true
				unionName = m[1]
				unionFields = []string{}

				// Inline members on same line? (e.g., `union U { A, B }`)
				if strings.Contains(line, "{") && strings.Contains(line, "}") {
					inside := line[strings.Index(line, "{")+1 : strings.Index(line, "}")]
					for _, f := range strings.Split(inside, ",") {
						field := strings.TrimSpace(f)
						if field != "" {
							unionFields = append(unionFields, field)
						}
					}
					unionDefs[unionName] = UnionDef{Name: unionName, Members: unionFields}
					inUnion = false
				}
				continue
			}
		} else {
			// Inside a multi-line union block
			if reUnionEnd.MatchString(line) {
				// Union ends
				unionDefs[unionName] = UnionDef{
					Name:    unionName,
					Members: unionFields,
				}
				inUnion = false
				unionName = ""
				unionFields = nil
				continue
			}

			// Accumulate union members
			line = strings.TrimSuffix(line, ",")
			line = strings.TrimSpace(line)
			if line != "" {
				unionFields = append(unionFields, line)
			}
		}
	}

	return scanner.Err()
}

// Second pass: generate JSON struct definitions for tables/structs and Decode helpers for union fields
func processFile(path string) ([]string, error) {
	var output []string
	var currentStruct []string
	var decodeMethods []string
	inStruct := false
	structName := ""

	file, err := os.Open(path)
	if err != nil {
		return nil, err
	}
	defer file.Close()

	scanner := bufio.NewScanner(file)
	reStruct := regexp.MustCompile(`^(struct|table)\s+(\w+)\s*\{`)
	reField := regexp.MustCompile(`^(\s*)(\w+):\s*([\[\]\w<>]+);`)

	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())

		if m := reStruct.FindStringSubmatch(line); m != nil {
			inStruct = true
			structName = m[2]
			currentStruct = []string{}
			decodeMethods = []string{}
			continue
		}

		if inStruct {
			if line == "}" {
				inStruct = false

				// Output struct type
				output = append(output, fmt.Sprintf("type %sJSON struct {", structName))
				output = append(output, currentStruct...)
				output = append(output, "}")
				output = append(output, "")

				// Output helper decode methods for union fields
				output = append(output, decodeMethods...)
				output = append(output, "")
				continue
			}

			if m := reField.FindStringSubmatch(line); m != nil {
				fieldNameOrig := m[2]
				fieldName := strings.Title(fieldNameOrig)
				fbType := m[3]

				baseType := strings.Trim(fbType, "[]")
				if _, isUnion := unionDefs[baseType]; isUnion {
					// For union fields, generate two sibling fields:
					// e.g. EvalType string `json:"eval_type"`
					//      Eval json.RawMessage `json:"eval"`

					typeFieldName := fieldName + "Type"
					typeJSONName := strings.ToLower(fieldNameOrig) + "_type"
					valueFieldName := fieldName
					valueJSONName := strings.ToLower(fieldNameOrig)

					currentStruct = append(currentStruct, fmt.Sprintf("    %s string `json:\"%s\"`", typeFieldName, typeJSONName))
					currentStruct = append(currentStruct, fmt.Sprintf("    %s json.RawMessage `json:\"%s\"`", valueFieldName, valueJSONName))

					// Generate helper Decode<Field>() method for this union field
					methodName := fmt.Sprintf("Decode%s", valueFieldName)
					unionName := baseType
					members := unionDefs[unionName].Members

					var methodLines []string
					methodLines = append(methodLines, fmt.Sprintf("func (o *%sJSON) %s() (interface{}, error) {", structName, methodName))
					methodLines = append(methodLines, fmt.Sprintf("    switch o.%s {", typeFieldName))
					for _, member := range members {
						methodLines = append(methodLines, fmt.Sprintf("    case \"%s\":", member))
						methodLines = append(methodLines, fmt.Sprintf("        var v %sJSON", member))
						methodLines = append(methodLines, fmt.Sprintf("        if err := json.Unmarshal(o.%s, &v); err != nil {", valueFieldName))
						methodLines = append(methodLines, "            return nil, err")
						methodLines = append(methodLines, "        }")
						methodLines = append(methodLines, "        return v, nil")
					}
					methodLines = append(methodLines, "    default:")
					methodLines = append(methodLines, fmt.Sprintf("        return nil, fmt.Errorf(\"unknown %s: %%s\", o.%s)", typeFieldName, typeFieldName))
					methodLines = append(methodLines, "    }")
					methodLines = append(methodLines, "}")
					decodeMethods = append(decodeMethods, strings.Join(methodLines, "\n"))
				} else {
					// Normal field
					goType := convertType(fbType)
					jsonTag := strings.ToLower(fieldNameOrig)
					currentStruct = append(currentStruct, fmt.Sprintf("    %s %s `json:\"%s\"`", fieldName, goType, jsonTag))
				}
			}
		}
	}

	return output, nil
}

func generateEnumUnmarshalFuncs() []string {
	var output []string
	for _, enumName := range enumList {
		funcLines := []string{
			fmt.Sprintf("func (e *%s) UnmarshalJSON(data []byte) error {", enumName),
			"    var s string",
			"    if err := json.Unmarshal(data, &s); err != nil {",
			"        return err",
			"    }",
			fmt.Sprintf("    if val, ok := EnumValues%s[s]; ok {", enumName),
			"        *e = " + enumName + "(val)",
			"        return nil",
			"    }",
			fmt.Sprintf("    return fmt.Errorf(\"invalid %s %%q\", s)", enumName),
			"}",
			"",
		}
		output = append(output, strings.Join(funcLines, "\n"))
	}
	return output
}

func main() {
	schemaDir := "../fbs-schema" // ← change to your actual schema path
	outputPath := "./schema/dd/wls/json.go"

	files, err := filepath.Glob(filepath.Join(schemaDir, "*.fbs"))
	if err != nil {
		panic(err)
	}

	// First pass: collect enums and unions
	for _, file := range files {
		if err := findEnumsAndUnions(file); err != nil {
			fmt.Fprintf(os.Stderr, "Error scanning %s: %v\n", file, err)
			continue
		}
	}

	var allStructs []string

	// We do NOT generate union wrapper structs anymore, unions handled by Decode helpers

	// Structs/tables
	for _, file := range files {
		structs, err := processFile(file)
		if err != nil {
			fmt.Fprintf(os.Stderr, "Error processing %s: %v\n", file, err)
			continue
		}
		allStructs = append(allStructs, structs...)
	}

	// Generate enum UnmarshalJSON funcs
	enumFuncs := generateEnumUnmarshalFuncs()
	allStructs = append(allStructs, enumFuncs...)

	// Write to file
	outFile, err := os.Create(outputPath)
	if err != nil {
		panic(err)
	}
	defer outFile.Close()

	fmt.Fprintln(outFile, "// Code generated by the FlatBuffers to JSON generator.")
	fmt.Fprintln(outFile, "package wls")
	fmt.Fprintln(outFile)
	fmt.Fprintln(outFile, "import (")
	fmt.Fprintln(outFile, "    \"encoding/json\"")
	fmt.Fprintln(outFile, "    \"fmt\"")
	fmt.Fprintln(outFile, ")")
	fmt.Fprintln(outFile)

	for _, line := range allStructs {
		fmt.Fprintln(outFile, line)
	}

	fmt.Printf("✅ Generated %d structs and enum unmarshal funcs at %s\n", len(allStructs), outputPath)
}
