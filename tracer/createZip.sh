cd bin/tracer-home
rm /Users/maxime.david/dd/layerTest/datadog.zip
rm -rf /tmp/dd-tracer
mkdir -p /tmp/dd-tracer/datadog
cp -R * /tmp/dd-tracer/datadog
cd /tmp/dd-tracer
zip -r -X /Users/maxime.david/dd/layerTest/datadog.zip datadog
