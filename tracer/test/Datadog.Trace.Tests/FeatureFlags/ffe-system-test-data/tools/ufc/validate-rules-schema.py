#!/usr/bin/env python3
"""Validate authored rules catalog shape; semantics live in the generator."""
import json
import sys
from pathlib import Path

try:
    import jsonschema
except ImportError:
    print("Install schema validation dependency: python3 -m pip install jsonschema==4.26.0", file=sys.stderr)
    sys.exit(2)


def main():
    root = Path(__file__).resolve().parents[2]
    inputs = {
        "rules-resources/catalog.json": "schemas/rules-resource-catalog.schema.json",
    }
    failed = False
    for document, schema_path in inputs.items():
        try:
            schema = json.loads((root / schema_path).read_text())
            jsonschema.Draft202012Validator.check_schema(schema)
            validator = jsonschema.Draft202012Validator(schema, format_checker=jsonschema.FormatChecker())
            errors = sorted(validator.iter_errors(json.loads((root / document).read_text())), key=lambda error: str(list(error.absolute_path)))
            for error in errors:
                print(f"{document}:{'/'.join(map(str, error.absolute_path))}: {error.message}", file=sys.stderr)
            failed |= bool(errors)
        except (OSError, ValueError, jsonschema.exceptions.SchemaError) as error:
            print(f"{document}: {error}", file=sys.stderr)
            failed = True
    if not failed:
        print("Rules catalog schema valid")
    return int(failed)


if __name__ == "__main__":
    sys.exit(main())
