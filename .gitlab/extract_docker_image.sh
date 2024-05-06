#Extracts docker image content to a target folder
set -eu

image="$1"
target_dir="$2"

mkdir --parent $target_dir

echo "Extracting Docker base image $image to folder $target_dir"
docker pull $image
docker save -o $target_dir/image.tar $image 
tar xf $target_dir/image.tar -C $target_dir
layers=$(jq -r '.[0].Layers[]' $target_dir/manifest.json)
 
for i in $layers; do
    tar xf $target_dir/$i -C $target_dir
done 

#Done! clean
rm -rf $target_dir/image.tar $target_dir/manifest.json $target_dir/oci-layout  $target_dir/index.json 
rm -rf $target_dir/blobs/