## What is this?

The [lib-injection project](https://github.com/DataDog/architecture/blob/mcculls/onboarding-tracer-lib-injection/rfcs/onboarding-tracer-lib-injection/rfc.md) is a tool to allow injection of the tracer into a customer's application container without requiring them to modify their application images.  

Here is the [public documentation](https://docs.datadoghq.com/tracing/trace_collection/admission_controller/).



### Content

The `ìmage` folder contains the actual init container docker file. It will be the one injected by the agent admission controller.

`pod.yml` contains a pod configuration for testing. It contains a config similar to what the admission controller will inject. It also contains the app itself. We use a sample app from MS for this test.

`values.yml` is the file to install the cluster agent following these instructions with [helm](https://docs.datadoghq.com/containers/kubernetes/installation/?tab=helm)

## Local testing

Here are some instructions for working with the init image for local development (i.e. when working on the init image itself).
Replace any namespaces with your own, including in `pod.yml`.

### Minikube

First, you can use [minikube](https://minikube.sigs.k8s.io/docs/start/) to test locally.
If you use it, after setup, run ```eval $(minikube docker-env)``` to reuse Minikube’s built-in Docker daemon. Reusing the built-in daemon means you don’t have to build a Docker registry on your host machine and push the image into it. Instead, you can build inside the same Docker daemon as Minikube, which speeds up local experiments.

### Kubernetes with Docker Desktop

Otherwise, uou can also use Kubernetes that comes with Docker Desktop:
>Docker Desktop includes a standalone Kubernetes server and client, as well as Docker CLI integration that runs on your machine. The Kubernetes server runs locally within your Docker instance, is not configurable, and is a single-node cluster.


### Building init image

```
cd image
# docker login
docker build . --tag $alias/test-auto-inst:latest
docker push $alias/test-auto-inst:latest
```

### Running with kubernetes

```
kubectl delete pod sample-app
kubectl apply -f pod.yml

# do the following 3 commands in separate terminal windows
kubectl logs -f sample-app
kubectl port-forward sample-app 18080:18080
curl localhost:18080
```
