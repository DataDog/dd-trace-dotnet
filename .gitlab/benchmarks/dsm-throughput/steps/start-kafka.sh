
#!/usr/bin/env bash

set -e

echo "Starting Kafka"

export KAFKA_LISTENERS="PLAINTEXT://:9092"
export KAFKA_ADVERTISED_LISTENERS="PLAINTEXT://kafka1:9092"
export ALLOW_PLAINTEXT_LISTENER="yes"
export KAFKA_ADVERTISED_HOST_NAME=kafka1
export KAFKA_ADVERTISED_PORT=9092
export KAFKA_PORT=9092
export KAFKA_BROKER_ID=1
export KAFKA_ZOOKEEPER_CONNECT=localhost:2181
export KAFKA_JMX_OPTS="-Xms256m -Xmx6144m -Dcom.sun.management.jmxremote -Dcom.sun.management.jmxremote.authenticate=false -Dcom.sun.management.jmxremote.ssl=false -Djava.rmi.server.hostname=0.0.0.0 -Dcom.sun.management.jmxremote.port=1099 -Dcom.sun.management.jmxremote.rmi.port=1099"
export JMX_PORT=1099
exec /usr/local/kafka/bin/kafka-server-start.sh /usr/local/kafka/config/server.properties
