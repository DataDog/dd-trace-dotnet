# generate_json.go

This file allows generating JSON schema from FlatBuffers definitions
You can call it using `go run tools/generate_json.go` from ./policies/go

Please note that this has been vibecoded and may require adjustments for your specific use case.
For instance it doesn't map to the enums for the union types. It has not been added to the Makefile
for this reason.

It also doesn't automatically generate the "default fallback" for enums.
