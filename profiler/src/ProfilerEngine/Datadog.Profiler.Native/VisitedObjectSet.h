// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <unordered_set>
#include <cstdint>

// Track visited objects to prevent cycles during traversal
class VisitedObjectSet {
private:
    std::unordered_set<uintptr_t> _visited;

public:
    bool IsVisited(uintptr_t address) const {
        return _visited.find(address) != _visited.end();
    }

    void MarkVisited(uintptr_t address) {
        _visited.insert(address);
    }

    void Clear() {
        _visited.clear();
    }

    size_t Size() const {
        return _visited.size();
    }
};
