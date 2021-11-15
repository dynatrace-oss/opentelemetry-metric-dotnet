using System.Collections.Generic;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests
{
	/// <summary>
	/// An exporter to be used for tests. It holds the exported items and export result internally.
	/// Useful to use for test assertion.
	/// </summary>
	internal class TestMetricReader : BaseExportingMetricReader
	{
		protected readonly BaseExporter<Metric> _exporter;
		private readonly List<Metric> _exportedItems = new();

		public TestMetricReader(BaseExporter<Metric> exporter)
			: base(exporter)
		{
			_exporter = exporter;
		}

		public ExportResult ExportResult { get; private set; }

		protected override bool ProcessMetrics(Batch<Metric> metrics, int timeoutMilliseconds)
		{
			foreach (var data in metrics)
			{
				_exportedItems.Add(data);
			}

			ExportResult = _exporter.Export(metrics);

			return ExportResult == ExportResult.Success;
		}

		internal List<Metric> GetExportedMetrics() => _exportedItems;
	}
}
