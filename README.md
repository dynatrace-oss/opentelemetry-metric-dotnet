# Dynatrace OpenTelemetry Metrics Exporter for .NET

> **DISCLAIMER**: This project was developed as part of an innovation day by Dynatrace R&D.
It is not complete, nor supported and only intended as a starting point for those wanting to ingest OpenTelemetry instrumented custom metrics into the Dynatrace platform.

This exporter plugs into the OpenTelemetry Metrics SDK for .NET, which is in alpha/preview state and neither considered stable nor complete as of this writing.

See [open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet) for the current state of the OpenTelemetry SDK for .NET.

## Getting started

The general setup of OpenTelemetry .NET is explained in the official [Getting Started Guide](https://github.com/open-telemetry/opentelemetry-dotnet/blob/0.8.0-beta/docs/trace/getting-started/README.md).

Once the Metrics API and SDK are developed further, instructions on using the API are expected to be added [here](https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/docs/metrics/getting-started.md).

To add the exporter to your project, run the following command or add the Nuget package yourself:

```sh
dotnet add package OpenTelemetry.Exporter.Dynatrace.Metrics
```

```csharp
// configure API endpoint and authentication token
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

A full setup is provided in our [example project](src/Examples.Console), which reads the parameters `url` and `apiToken` from the command line arguments.
By default, it tries to connect to a local OneAgent endpoint without authentication.

### Configuration

The exporter allows for configuring the following settings using the `DynatraceExporterOptions` object passed to the constructor:

#### Dynatrace API Endpoint

The endpoint to which the metrics are sent is specified using the `Url` property.

Given an environment ID `myenv123` on Dynatrace SaaS, the [metrics ingest endpoint](https://www.dynatrace.com/support/help/dynatrace-api/environment-api/metric-v2/post-ingest-metrics/) would be `https://myenv123.live.dynatrace.com/api/v2/metrics/ingest`.

If a OneAgent is installed on the host, it can provide a local endpoint for providing metrics directly without the need for an API token.
This feature is currently in an Early Adopter phase and has to be enabled as described in the [OneAgent metric API documentation](https://www.dynatrace.com/support/help/how-to-use-dynatrace/metrics/metric-ingestion/ingestion-methods/local-api/).
Using the local API endpoint, the host ID and host name context are automatically added to each metric as dimensions.
The default metric API endpoint exposed by the OneAgent is `http://localhost:14499/metrics/ingest`.

#### Dynatrace API Token

The Dynatrace API token to be used by the exporter is specified using the `ApiToken` property and could, for example, be read from an environment variable.

Creating an API token for your Dynatrace environment is described in the [Dynatrace API documentation](https://www.dynatrace.com/support/help/dynatrace-api/basics/dynatrace-api-authentication/).
The scope required for sending metrics is the `Ingest metrics` scope in the **API v2** section:

![API token creation](docs/img/api_token.png)

#### Metric Key Prefix

The `Prefix` property specifies an optional prefix, which is prepended to each metric key, separated by a dot (`<prefix>.<namespace>.<name>`).

#### Default Labels/Dimensions

The `Tags` property can be used to optionally specify a list of key/value pairs, which will be added as additional labels/dimensions to all data points.
