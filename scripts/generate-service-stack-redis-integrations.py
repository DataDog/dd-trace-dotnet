#!/usr/bin/env python3

from typing import Dict, List, Union
import urllib.request
import json


VERSION = "0.2.2.0"


class Signature(object):
    def __init__(self, types: List[str]):
        self.data = Signature.types_to_signature(types)

    @staticmethod
    def type_to_bytes(type_name: str) -> List[int]:
        if type_name.endswith("[]"):
            return [0x1D] + Signature.type_to_bytes(type_name[:len(type_name)-2])

        types = {
            "void": 0x01,
            "bool": 0x02,
            "char": 0x03,
            "sbyte": 0x04,
            "byte": 0x05,
            "short": 0x06,
            "ushort": 0x07,
            "int": 0x08,
            "uint": 0x09,
            "long": 0x0a,
            "ulong": 0x0b,
            "float": 0x0c,
            "double": 0x0d,
            "string": 0x0e,
            # ptr
            # byref
            # valuetype
            # class
            # var
            # mdarray
            # genericinst
            # typedref
            # native integer
            # native unsigned integer
            # FNPTR
            "object": 0x1c,
            # SZARRAY
            # MVAR
        }
        return [types[type_name]]

    @staticmethod
    def types_to_signature(types: List[str]) -> List[int]:
        return [0x00, len(types)-1] + sum((Signature.type_to_bytes(t) for t in types), [])

    def to_string(self) -> str:
        return " ".join("%02x" % x for x in self.data)


class MethodReplacement(object):
    def __init__(self, method_name: str, signature: Union[Signature, List[str]], caller: dict = None, target_signature: Union[Signature, List[str]] = None):
        self.name = method_name
        if isinstance(signature, Signature):
            self.signature = signature
        else:
            self.signature = Signature(signature)
        self.caller = caller

    def to_dict(self, integration: 'Integration', type_replacement: 'TypeReplacement') -> dict:
        caller = self.caller
        if caller is None:
            caller = {"assembly": integration.assembly}
        return {
            "caller": caller,
            "target": {
                "assembly": integration.assembly,
                "type": type_replacement.name,
                "method": self.name
            },
            "wrapper": {
                "assembly": f"Datadog.Trace.ClrProfiler.Managed, Version={VERSION}, Culture=neutral, PublicKeyToken=def86d061d0d2eeb",
                "type": f"Datadog.Trace.ClrProfiler.Integrations.{type_replacement.name}",
                "method": self.name,
                "signature": self.signature.to_string()
            }
        }


class TypeReplacement(object):
    def __init__(self, name: str, method_replacements: List[MethodReplacement]):
        self.name = name
        self.method_replacements = method_replacements

    def to_list(self, integration: 'Integration') -> list:
        return [x.to_dict(integration, self) for x in self.method_replacements]


class Integration(object):
    def __init__(self, name: str, assembly: str, type_replacements: List[TypeReplacement]):
        self.name = name
        self.assembly = assembly
        self.type_replacements = type_replacements

    def to_dict(self) -> dict:
        return {
            "name": self.name,
            "method_replacements": [
                x
                for tr in self.type_replacements
                for x in tr.to_list(self)
            ]
        }


integrations = [
    Integration("ServiceStackRedis", "ServiceStack.Redis", [
        TypeReplacement("ServiceStack.Redis.RedisNativeClient", [
            MethodReplacement("SendExpectCode", ["string", "object", "byte[][]"]),
            MethodReplacement("SendExpectComplexResponse", ["object", "object", "byte[][]"]),
            MethodReplacement("SendExpectData", ["byte[]", "object", "byte[][]"]),
            MethodReplacement("SendExpectDeeplyNestedMultiData", [
                              "object[]", "object", "byte[][]"]),
            MethodReplacement("SendExpectDouble", ["double", "object", "byte[][]"]),
            MethodReplacement("SendExpectLong", ["long", "object", "byte[][]"]),
            MethodReplacement("SendExpectMultiData", ["byte[][]", "object", "byte[][]"]),
            MethodReplacement("SendExpectSuccess", ["void", "object", "byte[][]"]),
            MethodReplacement("SendWithoutRead", ["void", "object", "byte[][]"]),
        ]),
    ]),
]


def main():
    print(json.dumps([i.to_dict() for i in integrations],
                     indent=2, separators=(',', ': ')))


if __name__ == "__main__":
    main()
