# Dynatrace OpenTelemetry Metrics Exporter for .NET

> This project is developed and maintained by Dynatrace R&D.
Currently, this is a prototype and not intended for production use.
It is not covered by Dynatrace support.

This exporter plugs into the OpenTelemetry Metrics SDK for .NET, which is in alpha/preview state and neither considered stable nor complete as of this writing.

See [open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet) for the current state of the OpenTelemetry SDK for .NET.

## Getting started

The general setup of OpenTelemetry .NET is explained in the official [Getting Started Guide](https://github.com/open-telemetry/opentelemetry-dotnet/blob/0.8.0-beta/docs/trace/getting-started/README.md).

Once the Metrics API and SDK are developed further, instructions on using the API are expected to be added [here](https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/docs/metrics/getting-started.md).

To add the exporter to your project, run the following command or add the Nuget package yourself:

```sh
dotnet add package Dynatrace.OpenTelemetry.Exporter.Metrics
```

This exporter package targets [.NET Standard 2.0](https://docs.microsoft.com/en-us/dotnet/standard/net-standard) and can therefore be included on .NET Core 2.0 and above, as well as .NET Framework 4.6.1 and above.

```csharp
// configure API endpoint and authentication token. It is suggested to not hard-code them here but to read them from the environment or from arguments.
var options = new DynatraceExporterOptions
{
    Url = url,
    ApiToken = apiToken,
};

// create exporter instance and pass an optional logger to be used
ILogger logger = loggerFactory.CreateLogger<DynatraceMetricsExporter>();

var dtExporter = new DynatraceMetricsExporter(options, logger);

// setup MeterProvider
MeterProvider.SetDefault(Sdk.CreateMeterProviderBuilder()
    .SetProcessor(processor)
    .SetExporter(dtExporter)
    .SetPushInterval(TimeSpan.FromSeconds(pushIntervalInSecs))
    .Build());

// get Meter, create a counter instrument and provide first data point
var meter = MeterProvider.Default.GetMeter("MyMeter");
var testCounter = meter.CreateInt64Counter("MyCounter");
var labels = new List<KeyValuePair<string, string>>()
{
    new KeyValuePair<string, string>("dimension-1", "value-1")
};
testCounter.Add(defaultContext, 100, meter.GetLabelSet(labels));
```

Optionally, it is possible to set additional properties on the `DynatraceExporterOptions` object:

```csharp
var options = new DynatraceExporterOptions
{
    // If URL and API token are not provided, the exporter will try to send metrics to the default OneAgent endpoint.
    // However, it is also possible to specify Url and ApiToken here.

    // *** Additional (optional) parameters: ***
    // A prefix that will be prepended to each exported metric.
    Prefix = "dynatrace.opentelemetry",

    // Default dimensions are added to each exported metric as "key=value" pairs.
    DefaultDimensions = new List<KeyValuePair<string, string>>{
        new KeyValuePair<string, string>("key1", "value1"),
        new KeyValuePair<string, string>("key2", "value2"),
    },

    // If a OneAgent is running on the same host as the application, it is possible to add host-specific data as dimensions automatically.
    OneAgentMetadataEnrichment = true,
};
```

A full setup is provided in our [example project](src/Examples.Console), which reads the parameters `url` and `apiToken` from the command line arguments.
By default, it tries to connect to a local OneAgent endpoint which requires no authentication.

### Configuration

The exporter allows for configuring the following settings using the `DynatraceExporterOptions` object passed to the constructor:

#### Dynatrace API Endpoint

If a OneAgent is installed on the host, it can provide a local endpoint for providing metrics directly without the need for an API token.
This feature is currently in an Early Adopter phase and has to be enabled as described in the [OneAgent metric API documentation](https://www.dynatrace.com/support/help/how-to-use-dynatrace/metrics/metric-ingestion/ingestion-methods/local-api/).
Using the local API endpoint, the host ID and host name context are automatically added to each metric as dimensions.
The default metric API endpoint exposed by the OneAgent is `http://localhost:14499/metrics/ingest`.

Alternatively, it is possible to specify the endpoint to which the metrics are sent using the `Url` property.
The metrics ingest endpoint URL looks like:

- `https://{your-environment-id}.live.dynatrace.com/api/v2/metrics/ingest` on SaaS deployments.
- `https://{your-domain}/e/{your-environment-id}/api/v2/metrics/ingest` on managed deployments.

#### Dynatrace API Token

The Dynatrace API token to be used by the exporter is specified using the `ApiToken` property and could, for example, be read from an environment variable.

Creating an API token for your Dynatrace environment is described in the [Dynatrace API documentation](https://www.dynatrace.com/support/help/dynatrace-api/basics/dynatrace-api-authentication/).
The scope required for sending metrics is the `Ingest metrics` scope in the **API v2** section:

![API token creation](docs/img/api_token.png)

#### Metric Key Prefix

The `Prefix` property specifies an optional prefix, which is prepended to each metric key, separated by a dot (`<prefix>.<namespace>.<name>`).

#### Default Dimensions

The `DefaultDimensions` property can be used to optionally specify a list of key/value pairs, which will be added as additional dimensions to all data points.

#### OneAgent metadata enrichment

If the `OneAgentMetadataEnrichment` property is set to true, the exporter attempts to [read metadata from a file](https://www.dynatrace.com/support/help/how-to-use-dynatrace/metrics/metric-ingestion/ingestion-methods/enrich-metrics/).
Metadata read that way will be added as dimensions to all exported metrics.

### Known issues and limitations

The OpenTelemetry Metrics SDK currently does not allow exporters to distinguish between values received from counters and those received from observers.
Counter values are passed to the exporter as deltas to the last export whereas for observers, the current value is reported.
For this exporter, we decided to properly support counters and thus send the received values marked as deltas, which will lead to wrong values being reported for observers, however.
