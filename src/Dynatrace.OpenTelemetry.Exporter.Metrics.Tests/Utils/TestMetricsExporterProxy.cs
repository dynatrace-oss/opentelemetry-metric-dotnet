using System.Collections.Generic;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests.Utils
{
	[AggregationTemporality(AggregationTemporality.Delta)]
	public class TestMetricsExporterProxy : BaseExporter<Metric>
	{
		private List<Metric> _exportedItems = new();
		private readonly BaseExporter<Metric> _exporter;

		public ExportResult ExportResult { get; private set; }

		public TestMetricsExporterProxy(BaseExporter<Metric> exporter)
		{
			_exporter = exporter;
		}

		public override ExportResult Export(in Batch<Metric> batch)
		{
			foreach (var data in batch)
			{
				_exportedItems.Add(data);
			}

			ExportResult = _exporter.Export(batch);
			return ExportResult;
		}

		protected override bool OnForceFlush(int timeoutMilliseconds)
		{
			return _exporter.ForceFlush(timeoutMilliseconds);
		}

		protected override bool OnShutdown(int timeoutMilliseconds)
		{
			return _exporter.Shutdown(timeoutMilliseconds);
		}

		protected override void Dispose(bool disposing)
		{
			_exporter.Dispose();
		}

		internal List<Metric> GetExportedMetrics()
		{
			var result = _exportedItems;
			_exportedItems = new List<Metric>();

			return result;
		}
	}
}
