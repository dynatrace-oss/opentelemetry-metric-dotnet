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
