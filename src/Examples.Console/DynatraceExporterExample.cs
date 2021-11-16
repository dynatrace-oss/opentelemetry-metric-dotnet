// <copyright company="Dynatrace LLC">
// Copyright 2020 Dynatrace LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Dynatrace.OpenTelemetry.Exporter.Metrics;
using System.Diagnostics.Metrics;

namespace Examples.Console
{
	internal class DynatraceExporterExample
	{
		private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder =>
		{
			builder.SetMinimumLevel(LogLevel.Debug).AddConsole();
		});

		private static readonly ILogger _logger = _loggerFactory.CreateLogger<DynatraceExporterExample>();

		internal static async Task<int> RunAsync(Options runOptions)
		{
			if (runOptions.Url == null)
			{
				_logger.LogInformation("no URL provided, falling back to default OneAgent endpoint.");
			}

			// A Meter instance is obtained via the System.Diagnostics.DiagnosticSource package
			// and not via the MeterProvider. To learn more, read here:
			// https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/metrics/getting-started/README.md
			using var meter = new Meter("my_meter", "0.0.1");

			// The application which decides to enable OpenTelemetry metrics
			// needs to setup a MeterProvider and register the Meters/Views at this point.
			// If meters are created later on, they can be registered upfront by using wildcards, e.g. AddMeter("MyApp.*")
			// More examples can be found on GitHub: https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/metrics
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddDynatraceExporter(cfg =>
				{
					cfg.ApiToken = runOptions.ApiToken;
					cfg.EnrichWithDynatraceMetadata = runOptions.EnableDynatraceMetadataEnrichment;
					cfg.DefaultDimensions = new Dictionary<string, string> { { "default1", "defval1" } };
					cfg.Prefix = "otel.dotnet";

					if (runOptions.Url != null)
					{
						cfg.Url = runOptions.Url;
					}
				}, _loggerFactory)
				.Build();

			var testCounter = meter.CreateCounter<long>("my_counter");
			var testHistogram = meter.CreateHistogram<long>("my_histogram");
			var testObserver = meter.CreateObservableCounter("my_observation", CallBackForMyObservation);

			var attributes1 = new TagList
			{
				{ "my_label", "value1" }
			};

			var attributes2 = new TagList
			{
				{ "my_label", "value2" }
			};

			var sw = Stopwatch.StartNew();
			while (sw.Elapsed.TotalMinutes < runOptions.DurationInMins)
			{
				testCounter.Add(100, attributes1);

				testHistogram.Record(100, attributes1);
				testHistogram.Record(500, attributes1);
				testHistogram.Record(5, attributes2);
				testHistogram.Record(750, attributes2);

				// The testObserver is called by the Meter automatically at each collection interval.
				await Task.Delay(1000);
				var remaining = (runOptions.DurationInMins * 60) - sw.Elapsed.TotalSeconds;
				_logger.LogInformation("Running and emitting metrics. Remaining time: {Remaining} seconds", (int)remaining);
			}

			_logger.LogInformation("Metrics exporter has shut down.");
			return 0;
		}

		internal static Measurement<long> CallBackForMyObservation()
		{
			var attributes = new TagList
			{
				{ "my_attribute", "value1" }
			};

			return new Measurement<long>(Process.GetCurrentProcess().WorkingSet64, attributes);
		}
	}
}
