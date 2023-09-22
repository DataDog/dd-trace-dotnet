#!/usr/bin/bash

/bin/bash ./install_timeit.sh

export PATH=~/.dotnet/tools/:$PATH

dotnet timeit Exceptions.linux.json

