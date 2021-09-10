# Datadog_Windows_Container
### About Repo: 

This repo contains an example of setting up Datadog .NET automatic tracing for a .NET 5 MVC application running in a Windows container. 

### How to use: 

- Clone/download this repository
- Update the `docker-compose.yml` by replacing the value for `DD_API_KEY`
- Run `docker-compose up` & navigate to `http://localhost:8000` 

#### Resulting .NET 5 APM traces within Datadog: 
![trace_view](https://user-images.githubusercontent.com/7599081/118568633-19d91300-b72d-11eb-8ea1-02ee10d388a0.png)
