# Runtime support policy for .NET APM

Datadog APM for .NET is built upon dependencies defined in specific versions of the host operating system, .NET runtime, certain .NET libraries, and the Datadog Agent/API. When these versions are no longer supported by their maintainers, Datadog APM for .NET limits its support for these as well.

## Levels of support

| **Level**                                              | **Support provided**                                                                                                                                                          |
|--------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| <span id="support-unsupported">Unsupported</span>      |  No implementation. [Contact our customer support team for special requests.](https://www.datadoghq.com/support/)                                                             |
| <span id="support-beta">Beta</span>                    |  Initial implementation. May not yet contain all features. Support for new features, bug & security fixes provided on a best-effort basis.                                    |
| <span id="support-ga">General Availability (GA)</span> |  Full implementation of all features. Full support for new features, bug & security fixes.                                                                                    |
| <span id="support-maintenance">Maintenance</span>      |  Full implementation of existing features. Does not receive new features. Support for bug & security fixes only.                                                              |
| <span id="support-legacy">Legacy</span>                |  Legacy implementation. May have limited function, but no maintenance provided. [Contact our customer support team for special requests.](https://www.datadoghq.com/support/) |
| <span id="support-eol">End-of-life (EOL)</span>        |  No support.                                                                                                                                                                  |

## Package versioning

Datadog APM for .NET practices [semantic versioning](https://semver.org/).

As this relates to downgrading runtime support, it implies:

  - **Major version updates** (e.g. `1.0.0` to `2.0.0`) may change support for any runtime from [Beta](#support-beta)/[GA](#support-ga) to [Maintenance](#support-maintenance)/[Legacy](#support-legacy)/[EOL](#support-eol).
  - **Minor version updates** (e.g. `1.0.0` to `1.1.0`) will not change support for any runtime.
  - **Patch version updates** (e.g. `1.0.0` to `1.0.1`) will not change support for any runtime.

## Supported .NET runtimes

| **Engine**      | **.NET version** | **Support level**     | **Package version**  | **MICROSOFT END OF LIFE** |
|-----------------|------------------|-----------------------|----------------------|---------------------------|
| .NET Core       | .NET 6           | [GA](#support-ga)     | Latest (>= `2.0.0`)  |                           |
|                 | .NET 5           | [GA](#support-ga)     | Latest (>= `1.27.0`) |                           |
|                 | .NET Core 3.1    | [GA](#support-ga)     | Latest               | 12/03/2022                |
|                 | .NET Core 2.1    | [GA](#support-ga)     | Latest               | 08/21/2021                |
| .NET FRAMEWORK  | 4.8              | [GA](#support-ga)     | Latest               |                           |
|                 | 4.7.2            | [GA](#support-ga)     | Latest               |                           |
|                 | 4.7              | [GA](#support-ga)     | Latest               |                           |
|                 | 4.6.2            | [GA](#support-ga)     | Latest               |                           |
|                 | 4.6.1            | [GA](#support-ga)     | Latest               |  04/26/2022               |
|                 | 4.6              | [EOL](#support-eol)   | < `2.0.0`            |  04/26/2022               |
|                 | 4.5.2            | [EOL](#support-eol)   | < `2.0.0`            |  04/26/2022               |
|                 | 4.5.1            | [EOL](#support-eol)   | < `2.0.0`            |  01/12/2016               |
|                 | 4.5.0            | [EOL](#support-eol)   | < `2.0.0`            |  01/12/2016               |

## Supported operating systems

| **OS**         | **Support level**     | **Package version** |
|----------------|-----------------------|---------------------|
| Linux x86-64   | [GA](#support-ga)     | Latest              |
| Windows x86-64 | [EOL](#support-ga)    | Latest              |
| Alpine         | [GA](#support-ga)     | Latest              |
| MacOS          | Dev environments only | Latest              |

## Supported Datadog Agent versions

| **Datadog Agent version**                                                | **Package version** |
|--------------------------------------------------------------------------|---------------------|
| [7.x](https://docs.datadoghq.com/agent/basic_agent_usage/?tab=agentv6v7) | Latest              |
| [6.x](https://docs.datadoghq.com/agent/basic_agent_usage/?tab=agentv6v7) | Latest              |
| [5.x](https://docs.datadoghq.com/agent/basic_agent_usage/?tab=agentv5)   | Latest              |

## Additional resources

```
DEV:  Consider adding/removing links to other useful resources here.
      Especially to where one may get more information about what's supported.
```

- [Datadog Customer support](https://www.datadoghq.com/support/)
- [Datadog APM for .NET Setup Documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/ruby/)
- [Datadog APM for .NET GitHub repository](https://github.com/DataDog/dd-trace-rb)