# Dynatrace OpenTelemetry Metrics Exporter for .NET

> This exporter is based on the OpenTelemetry Metrics SDK for .NET, which is currently in an alpha state and neither considered stable nor complete as of this writing.
> As such, this exporter is not intended for production use until the underlying OpenTelemetry Metrics API and SDK are stable.
> See [open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet) for the current state of the OpenTelemetry SDK for .NET.

## Getting started

The general setup of OpenTelemetry .NET is explained in the official [Getting Started Guide](https://github.com/open-telemetry/opentelemetry-dotnet/blob/0.8.0-beta/docs/trace/getting-started/README.md).

To add the exporter to your project add the [Dynatrace.OpenTelemetry.Exporter.Metrics](https://www.nuget.org/packages/Dynatrace.OpenTelemetry.Exporter.Metrics) package to your project.
This can be done through the NuGet package manager in Visual Studio or by running the following command in your project folder:

```sh
dotnet add package Dynatrace.OpenTelemetry.Exporter.Metrics
```

This exporter package targets [.NET Standard 2.0](https://docs.microsoft.com/en-us/dotnet/standard/net-standard) and can therefore be included on .NET Core 2.0 and above, as well as .NET Framework 4.6.1 and above.

### Setup

To set up a Dynatrace metrics exporter, add the following code to your project:

```csharp
// Create exporter instance. Without configuring anything, the exporter will attempt to export to the local OneAgent endpoint
var dtExporter = new DynatraceMetricsExporter();

// Set up OpenTelemetry by setting the global MeterProvider and attaching the exporter.
var processor = new UngroupedBatcher();
MeterProvider.SetDefault(Sdk.CreateMeterProviderBuilder()
    .SetProcessor(processor)
    .SetExporter(dtExporter)
    .SetPushInterval(TimeSpan.FromSeconds(30))  // set up the export interval
    .Build());

// Create a Meter and a counter instrument.
var meter = MeterProvider.Default.GetMeter("my_meter");
var myCounter = meter.CreateInt64Counter("my_counter");
var labels = new List<KeyValuePair<string, string>>()
{
    new KeyValuePair<string, string>("my_label", "value1")
};
var defaultContext = default(SpanContext);

// Record data. The export interval can be set up during the MeterProvider setup above.
// In the current configuration, all metrics will be exported to the local OneAgent endpoint every 30s.
myCounter.Add(defaultContext, 100, meter.GetLabelSet(labels));
```

If no local OneAgent is available or metrics should be exported directly to the backend, the `DynatraceMetricsExporter` can be set up with an endpoint and an API token.
The 'Ingest metrics' (`metrics.ingest`) permission is required for the token, and it is recommended to restrict the token access to that scope.
More information about the token setup can be found [here](#dynatrace-api-token).
The `DynatraceMetricsExporter` also accepts a logger, which logs information about preparing and exporting metrics.

```csharp
// Not required, but potentially helpful.
ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug).AddConsole();
});

// Set up a DynatraceExporterOptions object to set the endpoint and token.
// It is not recommended to store the token in code, but to read it from a secure location, e.g. environment variables or program arguments.
var exporterOptions = new DynatraceExporterOptions()
{
    Url = "https://{your-environment-id}.live.dynatrace.com/api/v2/metrics/ingest",
    ApiToken = "YOUR_API_TOKEN",
};

// The constructor for DynatraceMetricsExporter allows passing an DynatraceExporterOptions object and an optional logger.
var dtExporter = new DynatraceMetricsExporter(exporterOptions, loggerFactory.CreateLogger<DynatraceMetricsExporter>());

// Continue as above, set up OpenTelemetry and start exporting metrics:
var processor = new UngroupedBatcher();
MeterProvider.SetDefault(Sdk.CreateMeterProviderBuilder()
    .SetProcessor(processor)
    .SetExporter(dtExporter)
    .SetPushInterval(TimeSpan.FromSeconds(30))
    .Build());

var meter = MeterProvider.Default.GetMeter("my_meter");
var myCounter = meter.CreateInt64Counter("my_counter");
var labels = new List<KeyValuePair<string, string>>()
{
    new KeyValuePair<string, string>("my_label", "value1")
};
var defaultContext = default(SpanContext);

myCounter.Add(defaultContext, 100, meter.GetLabelSet(labels));
```

In addition to the `Url` and `ApiToken`, optional properties can be set on the `DynatraceExporterOptions` object, which are described in the [Configuration section](#configuration):

- A `Prefix`, that is prepended to every metric key.
- `DefaultDimensions`, which are added as dimensions to every exported metric
- A toggle, `EnrichWithOneAgentMetadata`, which allows turning off the enrichment of metrics with host-specific information.
    See [below](#dynatrace-api-endpoint) for more information.

```csharp
var exporterOptions = new DynatraceExporterOptions()
{
    Url = "https://{your-environment-id}.live.dynatrace.com/api/v2/metrics/ingest",
    ApiToken = "YOUR_API_TOKEN",
    Prefix = "metric.key.prefix",
    DefaultDimensions = new List<KeyValuePair<string, string>>()
    {
        new KeyValuePair<string, string>("defaultDim1", "value1")
        new KeyValuePair<string, string>("defaultDim2", "value2")
    },
    EnrichWithOneAgentMetadata = false,
};
```

## Example application

We provide an example command line application which exports metrics to Dynatrace.
To run it use

```sh
dotnet run --project src/Examples.Console/Examples.Console.csproj
```

Without any further configuration, the example app will try to export to a local OneAgent endpoint, which requires no authentication.
More information about the local OneAgent endpoint can be found [below](#dynatrace-api-endpoint).
The example app provides a number of command line options, which can be retrieved by running `dotnet run --project src/Examples.Console/Examples.Console.csproj -- --help`.
Note the `--` separating the dotnet command and the parameters passed to the application - everything after the dashes is passed to the application.

If no local OneAgent is available, the app can be configured with [an endpoint](#dynatrace-api-endpoint) and [a metrics ingest token](#dynatrace-api-token) like this:

```sh
dotnet run --project src/Examples.Console/Examples.Console.csproj -- -u "https://{your-environment-id}.live.dynatrace.com/api/v2/metrics/ingest" -t "YOUR_API_TOKEN"
```

## Configuration

The exporter allows for configuring the following settings using the `DynatraceExporterOptions` object passed to the constructor:

### Dynatrace API Endpoint

A OneAgent installed on the host can provide a local endpoint for ingesting metrics without the need for an API token.
The [OneAgent metric API documentation](https://www.dynatrace.com/support/help/how-to-use-dynatrace/metrics/metric-ingestion/ingestion-methods/local-api/) provides information on how to set up a local OneAgent endpoint.
Using the local API endpoint, the host ID and host name context are automatically added to each metric as dimensions.
If this is not desired, it can be turned off using the [`EnrichWithOneAgentMetadata` toggle](#export-oneagent-metadata).

If no OneAgent is running on the host or if metrics should be sent to a different endpoint, the `Url` property allows for setting that endpoint.

The [metrics ingest endpoint URL](https://www.dynatrace.com/support/help/dynatrace-api/environment-api/metric-v2/post-ingest-metrics/) looks like:

- `https://{your-environment-id}.live.dynatrace.com/api/v2/metrics/ingest` on SaaS deployments.
- `https://{your-domain}/e/{your-environment-id}/api/v2/metrics/ingest` on managed deployments.

### Dynatrace API Token

If metrics are not sent to the local OneAgent endpoint but directly to a Dynatrace server, an API token has to be provided for authentication.
The Dynatrace API token to be used by the exporter can be specified using the `ApiToken` property.
The token could, for example, be read from an environment variable or command line arguments.
It should not be hardcoded, especially if the code is stored in a VCS.

Creating an API token for your Dynatrace environment is described in the [Dynatrace API documentation](https://www.dynatrace.com/support/help/dynatrace-api/basics/dynatrace-api-authentication/).
The permission required for sending metrics is the `Ingest metrics` (`metrics.ingest`) permission in the **API v2** section
and it is recommended to limit scope to only this permission:

![API token creation](docs/img/api_token.png)

### Metric Key Prefix

The `Prefix` property allows specifying an optional prefix, which is prepended to each metric key, separated by a dot (e.g. a prefix of `<prefix>` and a metric name of `<name>` will lead to a combined metric name of `<prefix>.<name>`).

In the example, a prefix of `otel.dotnet` is used, which leads to metrics named `otel.dotnet.metric_name`, and allows for clear distinction between metrics from different sources in the Dynatrace metrics UI.

### Default Dimensions

The `DefaultDimensions` property can be used to optionally specify a `List<KeyValuePair<string, string>>`, which will be added as dimensions to all data points.
Dimension keys will be normalized, de-duplicated, and only one dimension value per key will be sent to the server.
Dimensions set on instruments will overwrite default dimensions if they share the same name after normalization.
[OneAgent metadata](#export-oneagent-metadata) will overwrite all dimensions described above, but it only uses Dynatrace-reserved keys starting with `dt.*`.

The reserved dimension `dt.metrics.source=opentelemetry` will automatically be added to every exported metric when using the exporter.

### Export OneAgent Metadata

If the `OneAgentMetadataEnrichment` property is set to true, the exporter will retrieve host and process metadata from the OneAgent, if available, and set it as dimensions to all exported metrics.
The `EnrichWithOneAgentMetaData` property on the options object can be used to disable OneAgent metadata export.
If running on a host with a OneAgent, setting this option will instruct the exporter to read and export metadata collected by the OneAgent to the Dynatrace endpoint.
This option is set to `true` by default.
If the OneAgent is running locally, but this option is set to false, no OneAgent metadata will be exported.
More information on the underlying OneAgent feature that is used by the exporter can be found in the
[Dynatrace documentation](https://www.dynatrace.com/support/help/how-to-use-dynatrace/metrics/metric-ingestion/ingestion-methods/enrich-metrics/).

## Known issues and limitations

The OpenTelemetry Metrics SDK currently does not allow exporters to distinguish between values received from counters and those received from observers.
Counter values are passed to the exporter as deltas to the last export whereas for observers, the current value is reported.
For this exporter, we decided to properly support counters and thus send the received values marked as deltas, which will lead to wrong values being reported for observers.
