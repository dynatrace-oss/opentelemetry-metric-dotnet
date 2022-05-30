// <copyright company="Dynatrace LLC">
// Copyright 2021 Dynatrace LLC
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

using System;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics
{
	public static class DynatraceMetricsExporterExtensions
	{
		/// <summary>
		/// Adds <see cref="DynatraceMetricsExporter"/> to the <see cref="MeterProviderBuilder"/>.
		/// </summary>
		/// <remarks>
		/// The exporter is configured together with a <see cref="PeriodicExportingMetricReader"/>.
		/// By default, the export interval is 1 minute (60000ms). It can be customized via the
		/// <see cref="DynatraceExporterOptions.MetricExportIntervalMilliseconds"/> property.
		/// </remarks>
		/// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
		/// <param name="configure">Exporter configuration options.</param>
		/// <param name="loggerFactory">The logger factory used to generate a ILogger instance for the exporter.</param>
		/// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
		public static MeterProviderBuilder AddDynatraceExporter(
			this MeterProviderBuilder builder,
			Action<DynatraceExporterOptions> configure = null,
			ILoggerFactory loggerFactory = null)
		{
			if (builder == null)
			{
				throw new ArgumentNullException(nameof(builder), "Must not be null");
			}

			return AddDynatraceExporter(builder, new DynatraceExporterOptions(), configure, loggerFactory);
		}

		private static MeterProviderBuilder AddDynatraceExporter(
			MeterProviderBuilder builder,
			DynatraceExporterOptions options,
			Action<DynatraceExporterOptions> configure = null,
			ILoggerFactory loggerFactory = null)
		{
			configure?.Invoke(options);

			ILogger<DynatraceMetricsExporter> logger = null;
			if (loggerFactory != null)
			{
				logger = loggerFactory.CreateLogger<DynatraceMetricsExporter>();
			}

			var metricExporter = new DynatraceMetricsExporter(options, logger);
			var metricReader = new PeriodicExportingMetricReader(metricExporter, options.MetricExportIntervalMilliseconds);
			metricReader.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
			return builder.AddReader(metricReader);
		}
	}
}
