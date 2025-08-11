package converter

import (
	"fmt"

	"github.com/DataDog/dd-policy-engine/go/schema"
	"github.com/DataDog/dd-policy-engine/go/schema/dd/wls"

	flatbuffers "github.com/google/flatbuffers/go"
)

type JSONCondition struct {
	Description string          `json:"description"`
	Values      []JSONPathValue `json:"values"`
}

func (c JSONCondition) ConvertToWLS(builder *flatbuffers.Builder) (flatbuffers.UOffsetT, error) {
	nodeCount := len(c.Values)
	if nodeCount == 0 {
		return 0, fmt.Errorf("no nodes to convert")
	}

	if nodeCount == 1 {
		return c.Values[0].ConvertToWLS(builder)
	}

	nodes := make([]flatbuffers.UOffsetT, len(c.Values))
	for i, value := range c.Values {
		node, err := value.ConvertToWLS(builder)
		if err != nil {
			return 0, err
		}
		nodes[i] = node
	}
	fmt.Printf("adding %d nodes\n", len(nodes))
	composite := schema.CompositeNodeCreate(builder, wls.BoolOperationBOOL_OR, c.Description, nodes)
	return schema.NodeTypeWrapperCreate(builder, composite, wls.NodeTypeCompositeNode), nil
}
