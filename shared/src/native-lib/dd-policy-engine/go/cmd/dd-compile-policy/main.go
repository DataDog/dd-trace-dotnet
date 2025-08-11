package main

import (
	"encoding/json"
	"fmt"
	"log"
	"os"

	"github.com/DataDog/dd-policy-engine/go/schema/dd/wls"
	flatbuffers "github.com/google/flatbuffers/go"
	"github.com/urfave/cli/v2"
)

func main() {
	app := &cli.App{
		Name:                 "dd-compile-policy",
		EnableBashCompletion: true,
		Usage:                "TODO",
		Commands: []*cli.Command{
			{
				Name:      "compile",
				Usage:     "Compile a new policy",
				UsageText: "usage: dd-compile-policy compile --input-json <input-json-string> --output <output>",
				Action:    Compile(),
				Flags: []cli.Flag{
					&cli.StringFlag{
						Name:  "input-json",
						Usage: "Specify the input JSON string",
					},
					&cli.StringFlag{
						Name:    "output",
						Usage:   "Specify the output file for the compiled policy",
						Aliases: []string{"o"},
					},
				},
			},
		},
	}
	err := app.Run(os.Args)
	if err != nil {
		log.Fatal(err)
	}
}

func Compile() cli.ActionFunc {
	return func(c *cli.Context) error {
		if !c.IsSet("input-json") || !c.IsSet("output") {
			return fmt.Errorf("usage: dd-compile-policy compile --input-json <input-json-string> --output <output>")
		}

		var jsonPolicies wls.PoliciesJSON
		err := json.Unmarshal([]byte(c.String("input-json")), &jsonPolicies)
		if err != nil {
			return fmt.Errorf("error unmarshalling: %v", err)
		}

		fbBuilder := flatbuffers.NewBuilder(0)

		policies, err := PoliciesFbsFromSchema(fbBuilder, jsonPolicies)
		fbBuilder.Finish(policies)
		buffer := fbBuilder.FinishedBytes()

		if err != nil {
			return fmt.Errorf("error creating FlatBuffers schema: %v", err)
		}

		if err := os.WriteFile(c.String("output"), buffer, 0644); err != nil {
			return fmt.Errorf("error writing output file: %v", err)
		}

		return nil
	}
}
