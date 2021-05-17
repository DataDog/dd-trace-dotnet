# Datadog_Windows_Container
### About Repo: 

This repo contains a(n) example of setting up Datadog .NET automatic tracing for a(n) .NET 5 MVC application running on Windows containers. 

### How to use: 

- Clone/download this repository
- Update the `docker-compose.yml` by replacing the value for `DD_API_KEY`
- Run `docker-compose up` & navigate to `http://localhost:8000` 

#### Resulting .NET 5 APM traces within Datadog: 
![flamegraph](https://i.ibb.co/pW419XH/Image-2021-05-17-at-11-30-58-AM.png)
