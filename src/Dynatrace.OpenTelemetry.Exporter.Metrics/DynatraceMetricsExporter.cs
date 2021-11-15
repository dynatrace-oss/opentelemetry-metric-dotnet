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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Dynatrace.MetricUtils;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics
{
	/// <summary>
	/// https://www.dynatrace.com/support/help/how-to-use-dynatrace/metrics/metric-ingestion/metric-ingestion-protocol/
	/// https://www.dynatrace.com/support/help/dynatrace-api/environment-api/metric-v2/post-ingest-metrics
	/// </summary>
	[AggregationTemporality(AggregationTemporality.Cumulative | AggregationTemporality.Delta, AggregationTemporality.Delta)]
	public class DynatraceMetricsExporter : BaseExporter<Metric>
	{
		private readonly DynatraceExporterOptions _options;
		private readonly ILogger<DynatraceMetricsExporter> _logger;
		private readonly HttpClient _httpClient;
		private readonly DynatraceMetricsSerializer _serializer;

		public DynatraceMetricsExporter(DynatraceExporterOptions options = null, ILogger<DynatraceMetricsExporter> logger = null)
			: this(options, logger, new HttpClient()) { }

		internal DynatraceMetricsExporter(DynatraceExporterOptions options, ILogger<DynatraceMetricsExporter> logger, HttpClient client)
		{
			_options = options ?? new DynatraceExporterOptions();
			_logger = logger ?? NullLogger<DynatraceMetricsExporter>.Instance;
			_logger.DynatraceMetricUrl(_options.Url);

			_httpClient = client;
			if (!string.IsNullOrEmpty(_options.ApiToken))
			{
				_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Api-Token", _options.ApiToken);
			}
			_httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue("opentelemetry-metric-dotnet")));

			_serializer = new DynatraceMetricsSerializer(
				_logger,
				_options.Prefix,
				_options.DefaultDimensions,
				metricsSource: "opentelemetry",
				enrichWithDynatraceMetadata: _options.EnrichWithDynatraceMetadata);
		}

		public override ExportResult Export(in Batch<Metric> batch)
		{
			// Prevents the exporter's HTTP operations from being instrumented.
			using (var scope = SuppressInstrumentationScope.Begin())
			{
				var metricLines = GetSerializeMetricLines(batch);

				// split all metrics into batches of DynatraceMetricApiConstants.PayloadLinesLimit lines
				var chunked = metricLines
					.Select((val, i) => new { val, batch = i / DynatraceMetricApiConstants.PayloadLinesLimit })
					.GroupBy(x => x.batch)
					.Select(x => x.Select(v => v.val));

				ExportResult exportResult;

				foreach (var chunk in chunked)
				{
					var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.Url);
					var joinedMetricLines = string.Join(Environment.NewLine, chunk);

					if (joinedMetricLines.Length == 0)
					{
						return ExportResult.Success;
					}

					httpRequest.Content = new StringContent(joinedMetricLines);
					try
					{
						// sync over async isn't great, but the exporter is executed in it's own thread.
						var response = _httpClient.SendAsync(httpRequest).GetAwaiter().GetResult();
						if (response.IsSuccessStatusCode)
						{
							exportResult = ExportResult.Success;
						}
						else
						{
							_logger.ReceivedErrorResponse(
								response.StatusCode, response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
							exportResult = ExportResult.Failure;
						}
					}
					catch (Exception ex)
					{
						_logger.FailedSendingMetricLines(ex);
						exportResult = ExportResult.Failure;
					}

					if (exportResult == ExportResult.Failure)
					{
						// if any batch fails, the entire batch should fail.
						return exportResult;
					}
				}

				return ExportResult.Success;
			}
		}

		private IReadOnlyCollection<string> GetSerializeMetricLines(in Batch<Metric> batch)
		{
			var metricLines = new List<string>();

			foreach (var metric in batch)
			{
				metricLines.AddRange(GetSerializedMetricPoints(metric));
			}
			return metricLines;
		}

		private IReadOnlyCollection<string> GetSerializedMetricPoints(Metric metric)
		{
			// TODO: Try to send a PR to get a .Count of the metric points
			// so we can allocate only the necessary in the list. Similar as in:
			// https://github.com/open-telemetry/opentelemetry-dotnet/pull/2542/files

			var lines = new List<string>();
			foreach (var metricPoint in metric.GetMetricPoints())
			{
				DynatraceMetric dtMetric = null;
				try
				{
					switch (metric.MetricType)
					{
						case MetricType.LongSum:
							dtMetric = metric.ToLongCounterDelta(metricPoint, _logger);
							break;
						case MetricType.DoubleSum:
							dtMetric = metric.ToDoubleCounterDelta(metricPoint, _logger);
							break;
						case MetricType.LongGauge:
							dtMetric = metric.ToLongGauge(metricPoint, _logger);
							break;
						case MetricType.DoubleGauge:
							dtMetric = metric.ToDoubleGauge(metricPoint, _logger);
							break;
						case MetricType.Histogram:
							dtMetric = metric.ToDoubleHistogram(metricPoint, _logger);
							break;
						default:
							_logger.InvalidMetricType(metric.MetricType);
							break;
					}
					if (dtMetric != null)
					{
						lines.Add(_serializer.SerializeMetric(dtMetric));
					}
				}
				catch (DynatraceMetricException dtEx)
				{
					_logger.FailedToSerializeMetric(metric.Name, dtEx);
				}
				catch (Exception ex)
				{
					_logger.FailedToSerializeMetric(metric.Name, ex);
				}
			}
			return lines;
		}
	}
}
