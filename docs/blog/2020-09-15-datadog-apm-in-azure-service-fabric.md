
# Deploy Datadog APM in .NET applications on Azure Service Fabric


<div style="text-align: right; font-style: italic;">date: 2020-09-15</div>

Datadog [Application Performance Monitoring (APM) and Distributed Tracing](https://docs.datadoghq.com/tracing/) products provide deep visibility into your applications. In order to get started, you need to configure the Datadog Agent and to instrument your application using the Datadog Tracer. This blog post describes how you can do this for a .NET application running in [Azure Service Fabric](https://azure.microsoft.com/services/service-fabric/).

<table border="1" bgcolor="#FFF0F0" align="center"><tr><td>

**The guidance described in this post is in an *alpha* state, and is not officially supported.**

However, if you try it, we would love to hear your feedback.
To leave us feedback, please [create a new issue](https://github.com/DataDog/dd-trace-dotnet/issues/new) and make sure to reference this article.
</td></tr></table>  
<p> </p> 

In this guide, we use the [.NET quick-start project](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-quickstart-dotnet) provided as a part of Microsoft's documentation on Azure Service Fabric. You can follow the [official docs](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-quickstart-dotnet) to deploy the quick-start project to the cloud. Here we will focus on how you can subsequently start collecting distributed traces of your application using Datadog.

More info on Azure Service Fabric:
 - [Documentation](https://docs.microsoft.com/azure/service-fabric/)
 - [Service Fabric sample projects](https://azure.microsoft.com/resources/samples/?service=service-fabric)
 - [Service Fabric open source home repo](https://github.com/azure/service-fabric)
 

## 1. Create a service fabric application

(*If you already have a service fabric application, you can skip ahead to [Set up the Datadog Agent](#set-up-the-datadog-agent)*).

One way to setup a Azure Service Fabric application is by starting a new project in Visual Studio:

![Create a service fabric project](https://user-images.githubusercontent.com/1801443/93098850-5079fd80-f675-11ea-90d6-7573b7faef68.png)

Start with a stateless ASP.NET Core application, and use MVC as a template.

![Create a stateless ASP.NET Core application](https://user-images.githubusercontent.com/1801443/93099063-959e2f80-f675-11ea-805c-eb627e2b9e53.png)

## 2. Set up the Datadog Agent

The Datadog Agent is a component that runs in a separate process next to each instance of your application. It collects telemetry about your infrastructure, merges it with your application telemetry (which is collected by the Tracer) and sends it all to the Datadog server.

In a Service Fabric we will use a standalone Docker container to include the Agent into our application. We will configure the container to run as a service available to other components of our application.

First, add a new Service Fabric Service to the solution:

![Add new Service Fabric Service](https://user-images.githubusercontent.com/1801443/93102030-04c95300-f679-11ea-89f2-1de6160b5bc2.png)

![Setup the agent container](https://user-images.githubusercontent.com/1801443/93107331-73111400-f67f-11ea-9a5e-06094e775177.png)

Next, replace the `ServiceManifest.xml` with the text below.

(As you do this, make sure to replace the value "*API_KEY_GOES_HERE*" for the `DD_API_KEY`-setting with your Datadog API key. An API key is a unique identifier that belongs to your organization and is required to submit telemetry to Datadog ([more info](https://docs.datadoghq.com/account_management/api-app-keys/)). You can obtain it from your Datadog [account page](https://app.datadoghq.com/account/settings#api).)
```
<?xml version="1.0" encoding="utf-8"?>
<ServiceManifest Name="DatadogAgentPkg" Version="1.0.0" xmlns="http://schemas.microsoft.com/2011/01/fabric" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <ServiceTypes>
    <StatelessServiceType ServiceTypeName="DatadogAgentType" UseImplicitHost="true" />
  </ServiceTypes>
  <CodePackage Name="Code" Version="1.0.0">
    <EntryPoint>
      <ContainerHost>
        <ImageName>datadog/agent</ImageName>
      </ContainerHost>
    </EntryPoint>
    <EnvironmentVariables>
      <EnvironmentVariable Name="DD_API_KEY" Value="API_KEY_GOES_HERE"/>
      <EnvironmentVariable Name="DD_APM_ENABLED" Value="true"/>
      <EnvironmentVariable Name="DD_APM_NON_LOCAL_TRAFFIC" Value="true"/>
      <EnvironmentVariable Name="DD_DOGSTATSD_NON_LOCAL_TRAFFIC" Value="true"/>
      <EnvironmentVariable Name="DD_HEALTH_PORT" Value="5002"/>
    </EnvironmentVariables>
  </CodePackage>
  <ConfigPackage Name="Config" Version="1.0.0" />
  <Resources>
    <Endpoints>
      <Endpoint Name="DatadogTypeEndpoint" UriScheme="http" Port="5002" Protocol="http"/>
      <Endpoint Name="DatadogTraceEndpoint" UriScheme="http" Port="8126" Protocol="http"/>
      <Endpoint Name="DatadogStatsEndpoint" UriScheme="udp" Port="8125" Protocol="udp"/>
    </Endpoints>
  </Resources>
</ServiceManifest>
```

Now, add the corresponding port bindings to the ServiceManfestImport within `ApplicationManifest.xml`:

```diff
  <ServiceManifestImport>
    <ServiceManifestRef ServiceManifestName="DatadogAgentPkg" ServiceManifestVersion="1.0.0" />
    <ConfigOverrides />
+   <Policies>
+     <ContainerHostPolicies CodePackageRef="Code">
+       <PortBinding ContainerPort="5002" EndpointRef="DatadogTypeEndpoint" />
+       <PortBinding ContainerPort="8126" EndpointRef="DatadogTraceEndpoint" />
+       <PortBinding ContainerPort="8125" EndpointRef="DatadogStatsEndpoint" />
+     </ContainerHostPolicies>
+   </Policies>
  </ServiceManifestImport>
```

## Install the .NET Tracer

The Datadog .NET Tracer is a also known as an in-process auto-instrumentation agent. It is a component that attaches to your application and automatically collects telemetry. The application telemetry is sent to the Datadog Agent (see above), where it is merged with infrastructure telemetry and forwarded to Datadog's servers. 

To install the .NET Tracer on our Service Fabric cluster, we need to include some scripts with each service deployed into the Fabric.

The tracer requires machine administrator permissions.
Add the *SetupAdminUser* to the *Principals* section in the `ApplicationManifest.xml`. If the *Principals* section is missing, add it:

```
  <Principals>
    <Users>
      <User Name="SetupAdminUser">
        <MemberOf>
          <SystemGroup Name="Administrators" />
        </MemberOf>
      </User>
    </Users>
  </Principals>
```

Within the *ServiceManifestImport* section of the service responsible for deploying the tracer to the cluster, give the Setup script SetupAdminUser as the executing user.

```diff
  <ServiceManifestImport>
    <ServiceManifestRef ServiceManifestName="ServiceThatDeploysDatadogTracerPkg" ServiceManifestVersion="1.0.0" />
    <ConfigOverrides />
+   <Policies>
+     <RunAsPolicy CodePackageRef="Code" UserRef="SetupAdminUser" EntryPointType="Setup" />
+   </Policies>
  </ServiceManifestImport>
```  

Within the ServiceManifest.xml of the service responsible for deploying the Datadog Tracer, add the reference to the install script.
```diff
  <CodePackage Name="Code" Version="1.0.0">
+   <SetupEntryPoint>
+     <ExeHost>
+       <Program>DatadogInstall.bat</Program>
+       <WorkingFolder>CodePackage</WorkingFolder>
+     </ExeHost>
+   </SetupEntryPoint>
    <EntryPoint>
      <ExeHost>
        <Program>ServiceThatDeploysDatadogTracer.exe</Program>
        <WorkingFolder>CodePackage</WorkingFolder>
      </ExeHost>
    </EntryPoint>
  </CodePackage>
```

Include the `DatadogInstall.bat` and `DatadogInstall.ps1` scripts in the project responsible for deploying the tracer.
Configure both files to be copied to the output directory.

![Copy to output directory](https://user-images.githubusercontent.com/1801443/93110062-d05a9480-f682-11ea-8fb4-7b266f576f68.png)

The latest representation of this install process is here: https://github.com/DataDog/azureservicefabric-dotnet-tracing-sample
 - [Batch Script](https://github.com/DataDog/azureservicefabric-dotnet-tracing-sample/blob/master/VotingWeb/DatadogInstall.bat)
 - [Powershell Script](https://github.com/DataDog/azureservicefabric-dotnet-tracing-sample/blob/master/VotingWeb/DatadogInstall.ps1)


### That's all folks
---

The next time you deploy this application to the cluster, your application should start sending traces to Datadog.
This also enables custom statistics through the DogStatsD client and custom traces through the Datadog.Trace library.

Happy developing!

---
