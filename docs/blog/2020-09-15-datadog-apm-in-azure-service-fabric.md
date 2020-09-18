
# Datadog .NET APM in Service Fabric

*This guidance is in an alpha state, and is not officially supported*

*However, if you try it, we would love to hear your feedback.*

This article contains guidance for enabling APM on .NET applications built with [Microsoft Azure Service Fabric](https://azure.microsoft.com/services/service-fabric/). The quickstart project contains a single application with multiple services demonstrating the basic concepts of service communication and use of reliable dictionaries.

For a more in depth tour with the sample used for this demo, visit: [Service Fabric .NET quickstart](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-quickstart-dotnet)

More info on Service Fabric:
 - [Documentation](https://docs.microsoft.com/azure/service-fabric/)
 - [Service Fabric sample projects](https://azure.microsoft.com/resources/samples/?service=service-fabric)
 - [Service Fabric open source home repo](https://github.com/azure/service-fabric)
 
There are two particular requirements for enabling Datadog in Azure Service Fabric: Deploying the Datadog Agent, and installing the .NET Tracer.

The Datadog Agent is a standalone Docker container which exists as a service.

The .NET tracer requires including some scripts with a deployed service to install the .NET Tracer on your clusters.
 
## Create a service fabric application

If you already have a service fabric application, you can skip ahead to `Setting up the datadog agent`.

Otherwise, here is one way to setup a Service Fabric Application in Visual Studio:

![Create a service fabric project](https://user-images.githubusercontent.com/1801443/93098850-5079fd80-f675-11ea-90d6-7573b7faef68.png)

Start with a stateless ASP.NET Core application, and use MVC as a template.

![Create a stateless ASP.NET Core application](https://user-images.githubusercontent.com/1801443/93099063-959e2f80-f675-11ea-805c-eb627e2b9e53.png)

## Setting up the Datadog agent

Add a new Service Fabric Service to the solution.

![Add new Service Fabric Service](https://user-images.githubusercontent.com/1801443/93102030-04c95300-f679-11ea-89f2-1de6160b5bc2.png)

![Setup the agent container](https://user-images.githubusercontent.com/1801443/93107331-73111400-f67f-11ea-9a5e-06094e775177.png)

Replace the ServiceManifest.xml with this, being sure to replace `API_KEY_GOES_HERE` with your Datadog API key:
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

Add the corresponding port bindings to the ServiceManfestImport within ApplicationManifest.xml

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

## Installing the .NET Tracer

The tracer requires machine administrator permissions.
Add the SetupAdminUser to the Principals section in the ApplicationManifest.xml. If the Principals section is missing, add it.

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

Within the ServiceManifestImport of the service responsible for deploying the tracer to the cluster, give the Setup script SetupAdminUser as the executing user.

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
