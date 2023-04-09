#!/bin/bash

FILE=$1.linux.$2.json
if [ -f "$FILE" ]; then
    ${GOPATH}/bin/timeit $FILE
else
    echo "$FILE does not exist."
fi
