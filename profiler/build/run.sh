#!/bin/bash

${GOPATH}/bin/timeit PiComputation.linux.$1.json
${GOPATH}/bin/timeit Exceptions.linux.$1.json
