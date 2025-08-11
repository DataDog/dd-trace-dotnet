package converter

import flatbuffers "github.com/google/flatbuffers/go"

type Converter interface {
	ConvertToWLS(builder *flatbuffers.Builder) (flatbuffers.UOffsetT, error)
}
