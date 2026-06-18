# Reference Tree Serialization Formats

The reference tree captures GC root retention chains as a type-level tree.
It is serialized alongside the `.pprof` payload and sent to the Datadog backend.

## Configuration

The env var `DD_INTERNAL_PROFILING_HEAPSNAPSHOT_REFERENCE_TREE_FORMAT` controls
which format(s) are emitted. It is a **bitfield integer**:

| Value | Meaning |
|-------|---------|
| `1`   | Binary only (default) |
| `2`   | JSON only |
| `3`   | Both binary and JSON |

Setting `3` is useful for validation tests that compare the two outputs.

---

## Format 1: JSON (v1)

Filename: `reference_tree.json`

```json
{
  "v": 1,
  "tt": ["System.String", "System.Object[]", "MyApp.Order"],
  "r": [
    {
      "t": 0, "c": "K", "ic": 42, "ts": 1680,
      "ch": [
        { "t": 2, "ic": 10, "ts": 512,
          "ch": [{ "t": 1, "ic": 5, "ts": 200 }] }
      ]
    },
    { "t": 2, "c": "S", "ic": 100, "ts": 8000, "fn": "_staticOrders" }
  ]
}
```

### Fields

| Key   | Scope      | Type     | Description |
|-------|------------|----------|-------------|
| `v`   | top-level  | int      | Format version (currently `1`) |
| `tt`  | top-level  | string[] | Type string table; nodes reference types by index |
| `r`   | top-level  | array    | Root nodes |
| `t`   | node/root  | uint32   | Index into `tt` |
| `c`   | root only  | string   | Root category code (see table below) |
| `ic`  | node/root  | uint64   | Instance count at this tree position |
| `ts`  | node/root  | uint64   | Total size in bytes |
| `fn`  | root only  | string?  | Static field name (only for `"c":"S"` roots) |
| `ch`  | node/root  | array?   | Children (recursive, omitted when empty) |

### Root Category Codes

| Code | Category |
|------|----------|
| `K`  | Stack |
| `S`  | StaticVariable |
| `F`  | Finalizer |
| `H`  | Handle |
| `P`  | Pinning |
| `W`  | ConditionalWeakTable |
| `R`  | COM |
| `O`  | Other |
| `?`  | Unknown |

---

## Format 2: Binary (v1) — Varint DFS

Filename: `reference_tree.bin`

All integers are encoded as **unsigned LEB128 varints** (identical to .NET's
`BinaryWriter.Write7BitEncodedInt` / Protocol Buffers varint encoding).
This makes the format byte-order-independent — no endianness issues on any
platform.

### Wire Layout

```
┌─────────────────────────────────────────────────┐
│ Header                                          │
│   magic        : 4 bytes fixed ("DDRT")         │
│   version      : varint (1)                     │
│   type_count   : varint                         │
│   root_count   : varint                         │
├─────────────────────────────────────────────────┤
│ String Table (one entry per type)               │
│   name_len     : varint (byte count)            │
│   name_bytes   : uint8[name_len]  (UTF-8)       │
├─────────────────────────────────────────────────┤
│ Root Nodes (DFS pre-order)                      │
│   type_index   : varint                         │
│   category     : uint8 (RootCategory ordinal)   │
│   inst_count   : varint                         │
│   total_size   : varint                         │
│   field_len    : varint (0 = no field name)     │
│   field_bytes  : uint8[field_len]  (UTF-8)      │
│   child_count  : varint                         │
│   [children follow inline in DFS order]         │
├─────────────────────────────────────────────────┤
│ Tree Nodes (recursive children)                 │
│   type_index   : varint                         │
│   inst_count   : varint                         │
│   total_size   : varint                         │
│   child_count  : varint                         │
│   [children follow inline in DFS order]         │
└─────────────────────────────────────────────────┘
```

### Category Byte Values

The `category` field uses the `RootCategory` enum ordinal directly:

| Byte | Category |
|------|----------|
| 0    | Stack |
| 1    | StaticVariable |
| 2    | Finalizer |
| 3    | Handle |
| 4    | Pinning |
| 5    | ConditionalWeakTable |
| 6    | COM |
| 7    | Other |
| 8    | Unknown |

### Magic Bytes

The 4-byte magic `DDRT` (0x44 0x44 0x52 0x54) allows format auto-detection:
readers can peek the first 4 bytes to decide whether to use the binary or
JSON parser.

### Varint Encoding Reference

Each byte stores 7 data bits; the high bit means "more bytes follow":

| Value Range         | Bytes | Typical Use |
|---------------------|-------|-------------|
| 0 – 127             | 1     | type indices, small counts, child_count |
| 128 – 16,383        | 2     | most instance counts |
| 16,384 – 2,097,151  | 3     | moderate sizes |
| up to 2^64          | ≤ 10  | large total sizes |

---

## Evaluated Alternatives

Four binary serialization approaches were evaluated before choosing varint.

### Option A: Fixed-Width Records, DFS Pre-Order

Every numeric field uses a fixed C-struct size (uint32/uint64). Tree flattened
in DFS pre-order with a `child_count` field per node.

- **Pros:** Dead simple; mirrors existing `AllocationsRecorder` pattern;
  C# `BinaryReader` reads little-endian natively.
- **Cons:** Wastes space on small values (leaf node always 24 bytes);
  Java needs `ByteBuffer.order(LITTLE_ENDIAN)`.
- **Estimated size:** ~62 KB for 2K nodes (~40% less than JSON).

### Option B: Varint-Encoded Records, DFS Pre-Order (chosen)

Same DFS structure but all integers use unsigned LEB128 varint encoding.

- **Pros:** Best compression (~70% less than JSON); byte-order-independent;
  C# has built-in `Read7BitEncodedInt()`; Java needs only a 10-line helper;
  no new C++ dependencies.
- **Cons:** No random access within records; harder to hex-dump inspect.
- **Estimated size:** ~32 KB for 2K nodes.

### Option C: Flat Node Table + Parent Index

All nodes in a sequential array; each stores a `parent_index` instead of
embedding children. No recursion needed for deserialization.

- **Pros:** Flat loop deserialization; random access by index;
  C# can use `MemoryMarshal.Read<T>` for struct blitting.
- **Cons:** +4 bytes/node for parent_index; worst binary compression;
  requires DFS numbering pass on writer; two-pass tree reconstruction on reader.
- **Estimated size:** ~67 KB for 2K nodes (~35% less than JSON).

### Option D: Protocol Buffers

Standard `.proto` schema with generated readers in Java/C#.

- **Pros:** Zero reader code (fully generated); explicit schema contract;
  built-in forward/backward compatibility.
- **Cons:** No protobuf C++ dependency exists in the native profiler today —
  adding one affects all platform builds; ~50% larger than raw varint due to
  field tags and length-delimited nesting; nested messages require knowing
  serialized size before writing (forces temp buffers or two-pass).
- **Estimated size:** ~45 KB for 2K nodes (~55% less than JSON).

### Decision Rationale

Option B (varint) was chosen because:

1. **Best compression** — ~70% smaller than JSON, ~30% smaller than protobuf.
2. **C# built-in support** — `BinaryReader.Read7BitEncodedInt[64]()` decodes
   the exact same unsigned LEB128 encoding with zero custom code.
3. **No endianness issues** — varint is byte-order-independent; neither
   Java nor C# readers need byte-order configuration.
4. **No C++ dependencies** — the varint encoder is ~6 lines; no build system
   changes, no cross-platform library management.
5. **Natural DFS match** — the existing JSON serializer already walks in DFS
   pre-order; the binary serializer follows the same traversal.
