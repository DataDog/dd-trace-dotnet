# Generative AI Instrumentation

Generative AI integrations provide observability into LLM and AI model operations.

## What to Trace

### Core Model Operations (Critical)

**Chat/Completion**
- Chat completions: `chat.completions.create()`
- Text completions: `completions.create()`
- Message creation: `messages.create()`

**Embeddings**
- Embedding generation: `embeddings.create()`
- Document embedding: `embedDocuments()`
- Query embedding: `embedQuery()`

### Streaming Responses
- Streaming completions (same operation, stream mode)
- Each streaming request is one span (not per-chunk)

### Agent Operations
- Agent invocation/execution
- Tool/function calls made by agents
- Chain execution in frameworks like LangChain

### Specialized Operations
- Image generation
- Audio transcription/translation
- Multimodal content generation

## What to Skip

### Client Setup
- Client instantiation
- API key configuration
- Model configuration

### File Operations (Lower Priority)
- File uploads for fine-tuning
- File management operations
- These may have APM spans but not full LLM observability

### Administrative Operations
- Model listing
- Fine-tuning job management
- Usage/billing queries

### Internal Operations
- Tokenization
- Request validation
- Response parsing

## Context Propagation

Generative AI operations typically do NOT require context propagation - they are leaf spans that inherit context from the current trace.

For agent frameworks with tool calls, context flows through the agent's execution, not through the LLM API itself.

## Streaming Handling

Streaming responses require special handling:
- Collect chunks until stream completion
- Reconstruct full response for observability
- Single span covers entire streaming operation
- Token counts available at stream end
