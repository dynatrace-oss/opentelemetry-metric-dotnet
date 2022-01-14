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
