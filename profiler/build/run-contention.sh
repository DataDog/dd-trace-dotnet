#!/usr/bin/env bash

./install_timeit.sh

export PATH=~/.dotnet/tools/:$PATH

dotnet timeit Contention.linux.json

