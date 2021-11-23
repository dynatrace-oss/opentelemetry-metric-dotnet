# Dynatrace OpenTelemetry Metrics Exporter for .NET

> This exporter is based on the OpenTelemetry Metrics SDK for .NET, which is currently in an alpha state and neither considered stable nor complete as of this writing.
> As such, this exporter is not intended for production use until the underlying OpenTelemetry Metrics API and SDK are stable.
> See [open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet) for the current state of the OpenTelemetry SDK for .NET.

## Getting started

The general setup of OpenTelemetry .NET is explained in the official [Getting Started Guide](https://github.com/open-telemetry/opentelemetry-dotnet/blob/core-1.2.0-beta2/docs/trace/getting-started/README.md).

To add the exporter to your project, install the [Dynatrace.OpenTelemetry.Exporter.Metrics](https://www.nuget.org/packages/Dynatrace.OpenTelemetry.Exporter.Metrics) package to your project.
This can be done through the NuGet package manager in Visual Studio or by running the following command in your project folder:

```sh
dotnet add package Dynatrace.OpenTelemetry.Exporter.Metrics
```

This exporter package targets [.NET Standard 2.0](https://docs.microsoft.com/en-us/dotnet/standard/net-standard) and can therefore be included on .NET Core 2.0 and above, as well as .NET Framework 4.6.1 and above.

### Setup

To set up a Dynatrace metrics exporter, add the following code to your project:

```csharp
// A Meter instance is obtained via the System.Diagnostics.DiagnosticSource package
var meter = new Meter("my_meter", "0.0.1");

// Configure the MeterProvider with the DynatraceMetricsExporter
// using the extension method .AddDynatraceExporter().
// Without passing any configuration, the exporter will attempt 
// to export to the local OneAgent endpoint.
var provider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(meter.Name)
    .AddDynatraceExporter()
    .Build();

// Use the meter to create a counter instrument.
var myCounter = meter.CreateCounter<long>("my_counter");
var attributes = new TagList
{
    { "my_label", "value1" }
};

// Record a metric. The export interval can be configured during the AddDynatraceExporter() call above.
// By default, metrics are exported in 1 minute intervals (60000ms).
myCounter.Add(100, attributes);
```

If no local OneAgent is available or metrics should be exported directly to the backend, the `DynatraceMetricsExporter` can be set up with an endpoint and an API token.

The 'Ingest metrics' (`metrics.ingest`) permission is required for the token,
and it is recommended to restrict the token access to that scope.
More information about the token setup can be found [here](#dynatrace-api-token).

```csharp
// Not required, but potentially helpful.
// The exporter logs information about preparing and exporting metrics.
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug).AddConsole();
});

var meter = new Meter("my_meter", "0.0.1");

// Configure the MeterProvider with the DynatraceMetricsExporter
// using the extension method .AddDynatraceExporter().
// You can customize the exporter by providing an Action<DynatraceExporterOptions>.
using var provider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(meter.Name)
    .AddDynatraceExporter(cfg =>
    {
        cfg.Url = "https://{your-environment-id}.live.dynatrace.com/api/v2/metrics/ingest";
        cfg.ApiToken = "YOUR_API_TOKEN";
    }, loggerFactory)
    .Build();

// Use the meter to create a counter instrument.
var myCounter = meter.CreateCounter<long>("my_counter");
var attributes = new TagList
{
    { "my_label", "value1" }
};

// Record a metric which will be exported to the provided Url.
myCounter.Add(100, attributes);
```

The example below shows all the other optional properties that can be
configured in the `DynatraceExporterOptions`.
Read the [Configuration section](#configuration) to learn more about each of them.

```csharp
using var provider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(meter.Name)
    .AddDynatraceExporter(cfg =>
    {
        cfg.Prefix = "metric.key.prefix";
        cfg.DefaultDimensions = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("defaultDim1", "value1"),
            new KeyValuePair<string, string>("defaultDim2", "value2")
        };
        cfg.EnrichWithDynatraceMetadata = true;
        cfg.MetricExportIntervalMilliseconds = 10000;
    }, loggerFactory)
    .Build();
```

## Example application

We provide an example command line application which exports metrics to Dynatrace.
To run it, change into the folder and use `dotnet run`:

```sh
cd src/Examples.Console
dotnet run
```

Without any further configuration, the example app will try to export to a local OneAgent endpoint, which requires no authentication.
More information about the local OneAgent endpoint can be found [below](#dynatrace-api-endpoint).

The example app provides a number of command line options, which can be retrieved by running `dotnet run --project src/Examples.Console/Examples.Console.csproj -- --help`.

Note the `--` separating the dotnet command and the parameters passed to the application.
Everything after the dashes is passed to the application.

If no local OneAgent is available, the app can be configured with [an endpoint](#dynatrace-api-endpoint) and [a metrics ingest token](#dynatrace-api-token) like this:

```sh
cd src/Examples.Console
dotnet run -- -u "https://{your-environment-id}.live.dynatrace.com/api/v2/metrics/ingest" -t "YOUR_API_TOKEN"
```

## Configuration

The `DynatraceExporterOptions` class contains all the available configuration.
The `DynatraceExporterOptions` can be provided either via the `AddDynatraceExporter()` extension method
on the `MeterProviderBuilder`, or by manually passing it to the `DynatraceMetricsExporter` constructor.

The `DynatraceExporterOptions` class contains the following properties:

### Dynatrace API Endpoint (`Url`)

A OneAgent installed on the host can provide a local endpoint for ingesting metrics without the need for an API token.
The [OneAgent metric API documentation](https://www.dynatrace.com/support/help/how-to-use-dynatrace/metrics/metric-ingestion/ingestion-methods/local-api/) provides information on how to enable the local OneAgent endpoint, if necessary.
Using the local API endpoint, the host ID and host name context are automatically added to each metric as dimensions.

If no OneAgent is running on the host or if metrics should be sent to a different endpoint, the `Url` property allows for setting that endpoint.

The [metrics ingest endpoint URL](https://www.dynatrace.com/support/help/dynatrace-api/environment-api/metric-v2/post-ingest-metrics/) follows the format:

- `https://{your-environment-id}.live.dynatrace.com/api/v2/metrics/ingest` on SaaS deployments.
- `https://{your-domain}/e/{your-environment-id}/api/v2/metrics/ingest` on managed deployments.

### Dynatrace API Token (`ApiToken`)

If metrics are not sent to the local OneAgent endpoint but directly to a Dynatrace server, an API token has to be provided for authentication.
The Dynatrace API token to be used by the exporter can be specified using the `ApiToken` property.
The token could, for example, be read from an environment variable or command line arguments.
It should not be hardcoded, especially if the code is stored in a VCS.

Creating an API token for your Dynatrace environment is described in the [Dynatrace API documentation](https://www.dynatrace.com/support/help/dynatrace-api/basics/dynatrace-api-authentication/).
The permission required for sending metrics is the `Ingest metrics` (`metrics.ingest`) permission in the **API v2** section
and it is recommended to limit scope to only this permission:

![API token creation](docs/img/api_token.png)

### Metric Key Prefix (`Prefix`)

The `Prefix` property allows specifying an optional prefix, which is prepended to each metric key, separated by a dot (e.g. a prefix of `<prefix>` and a metric name of `<name>` will lead to a combined metric name of `<prefix>.<name>`).

In the example, a prefix of `otel.dotnet` is used, which leads to metrics named `otel.dotnet.metric_name`, and allows for clear distinction between metrics from different sources in the Dynatrace metrics UI.

### Default Dimensions (`DefaultDimensions`)

The `DefaultDimensions` property can be used to optionally specify a `List<KeyValuePair<string, string>>`, which will be added as dimensions to all data points.
Dimension keys will be normalized, de-duplicated, and only one dimension value per key will be sent to the server.
Dimensions set on instruments will overwrite default dimensions if they share the same name after normalization.
[OneAgent metadata](#export-oneagent-metadata) will overwrite all dimensions described above, but it only uses Dynatrace-reserved keys starting with `dt.*`.

The reserved dimension `dt.metrics.source=opentelemetry` will automatically be added to every exported metric when using the exporter.

### Enrich metrics with Dynatrace Metadata (`EnrichWithDynatraceMetadata`)

If the `EnrichWithDynatraceMetadata` property is set to true, the exporter will retrieve host and process metadata from the OneAgent, if available, and set it as dimensions to all exported metrics.
The `EnrichWithDynatraceMetadata` property on the options object can be used to disable Dynatrace metadata export.
If running on a host with a OneAgent, setting this option will instruct the exporter to read and export metadata collected by the OneAgent to the Dynatrace endpoint.
This option is set to `true` by default.
If the OneAgent is running locally, but this option is set to false, no Dynatrace metadata will be exported.
More information on the underlying OneAgent feature that is used by the exporter can be found in the
[Dynatrace documentation](https://www.dynatrace.com/support/help/how-to-use-dynatrace/metrics/metric-ingestion/ingestion-methods/enrich-metrics/).

### Export interval (`MetricExportIntervalMilliseconds`)

The interval to collect metrics. This value is passed and used by the 
[Periodic Metric Reader](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#periodic-exporting-metricreader).

This option is set to **1 minute** (60000ms) by default.

## Known issues and limitations

### Typed attributes support

The OpenTelemetry specification has a concept of
[Attributes](
https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/common/common.md#attributes).
These attributes consist of key-value pairs, where the keys are strings
and the values are either primitive types or arrays of uniform primitive types.

The OpenTelemetry .NET SDK implementation of this works with a `KeyValuePair<string, object>`, meaning the `value `can be of any type.

At the moment, this exporter **only supports attributes with a string value type**.
This means that if attributes of any other type are used,
they will be **ignored** and **only** the string-valued attributes
are going to be sent to Dynatrace.
