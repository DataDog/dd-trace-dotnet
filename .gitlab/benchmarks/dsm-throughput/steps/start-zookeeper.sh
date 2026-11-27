#!/usr/bin/env bash

set -e

export ALLOW_ANONYMOUS_LOGIN=yes
exec /usr/local/kafka/bin/zookeeper-server-start.sh /usr/local/kafka/config/zookeeper.properties