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
		/// By default, the export interval is 1000ms. It can be customized via the
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
			return builder.AddReader(metricReader);
		}
	}
}
