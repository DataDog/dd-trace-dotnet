package converter

import (
	"fmt"

	"github.com/DataDog/dd-policy-engine/go/schema"

	flatbuffers "github.com/google/flatbuffers/go"
)

type JSONPolicies struct {
	Policies []JSONPolicy `json:"policies"`
}

func (p JSONPolicies) ConvertToWLS(builder *flatbuffers.Builder) (flatbuffers.UOffsetT, error) {
	var policies []flatbuffers.UOffsetT
	for i, policy := range p.Policies {
		fmt.Printf("converting policy #%d\n", i)
		policy, err := policy.ConvertToWLS(builder)
		if err != nil {
			return 0, err
		}
		policies = append(policies, policy)
	}
	return schema.PoliciesCreate(builder, policies), nil
}
