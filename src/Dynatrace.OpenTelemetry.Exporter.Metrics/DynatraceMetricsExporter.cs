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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Metrics.Export;
using DynatraceMetric = Dynatrace.MetricUtils.Metric;
using DynatraceMetricSerializer = Dynatrace.MetricUtils.MetricsSerializer;
using DynatraceMetricException = Dynatrace.MetricUtils.MetricException;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics
{
	/// <summary>
	/// https://www.dynatrace.com/support/help/how-to-use-dynatrace/metrics/metric-ingestion/metric-ingestion-protocol/
	/// https://www.dynatrace.com/support/help/dynatrace-api/environment-api/metric-v2/post-ingest-metrics
	/// </summary>
	public class DynatraceMetricsExporter : MetricExporter
	{
		private readonly DynatraceExporterOptions _options;
		private readonly ILogger<DynatraceMetricsExporter> _logger;
		private readonly HttpClient _httpClient;
		private readonly DynatraceMetricSerializer _serializer;

		public DynatraceMetricsExporter(DynatraceExporterOptions options = null, ILogger<DynatraceMetricsExporter> logger = null)
		: this(options, logger, new HttpClient()) { }

		internal DynatraceMetricsExporter(DynatraceExporterOptions options, ILogger<DynatraceMetricsExporter> logger, HttpClient client)
		{
			_options = options ?? new DynatraceExporterOptions();
			_logger = logger ?? NullLogger<DynatraceMetricsExporter>.Instance;
			_logger.LogDebug("Dynatrace Metrics Url: {Url}", _options.Url);
			_httpClient = client;
			if (!string.IsNullOrEmpty(_options.ApiToken))
			{
				_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Api-Token", _options.ApiToken);
			}
			_httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue("opentelemetry-metric-dotnet")));
			_serializer = new DynatraceMetricSerializer(_logger, _options.Prefix, _options.DefaultDimensions, metricsSource: "opentelemetry", enrichWithDynatraceMetadata: _options.EnrichWithDynatraceMetadata);
		}

		public override async Task<ExportResult> ExportAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
		{
			// split all metrics into batches of DynatraceMetricApiConstants.PayloadLinesLimit lines
			var chunked = metrics
				.Select((val, i) => new { val, batch = i / DynatraceMetricApiConstants.PayloadLinesLimit })
				.GroupBy(x => x.batch)
				.Select(x => x.Select(v => v.val));

			var exportResults = new List<ExportResult>();

			foreach (var chunk in chunked)
			{
				var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.Url);
				var sb = new StringBuilder();

				foreach (var metric in chunk)
				{
					SerializeMetric(sb, metric);
				}

				var metricLines = sb.ToString();
				_logger.LogDebug(metricLines);
				httpRequest.Content = new StringContent(metricLines);
				try
				{
					var response = await _httpClient.SendAsync(httpRequest);
					if (response.IsSuccessStatusCode)
					{
						_logger.LogDebug("StatusCode: {StatusCode}", response.StatusCode);
						exportResults.Add(ExportResult.Success);
					}
					else
					{
						_logger.LogError("StatusCode: {StatusCode}", response.StatusCode);
						_logger.LogError("Content: {Content}", await response.Content.ReadAsStringAsync());
						exportResults.Add(ExportResult.FailedNotRetryable);
					}
				}
				catch (Exception e)
				{
					_logger.LogError("Error sending metrics: {Error}", e.Message);
					throw;
				}
			}

			// if all chunks were exported successfully, return success, otherwise failed.
			return exportResults.All(x => x == ExportResult.Success) ? ExportResult.Success : ExportResult.FailedNotRetryable;
		}

		private void SerializeMetric(StringBuilder sb, Metric metric)
		{
			IEnumerable<DynatraceMetric> dynatraceMetrics = DynatraceMetricsMapper.ToDynatraceMetric(metric);
			using (var metricsEnumerator = dynatraceMetrics.GetEnumerator())
			{
				var moveNext = true;
				while(moveNext)
				{
					try
					{
						moveNext = metricsEnumerator.MoveNext();
					}
					catch (DynatraceMetricException e)
					{
						_logger.LogWarning("Skipping metric with the original name '{}'. Mapping failed with message: {}", metric.MetricName, e.Message);
						continue;
					}
					if (moveNext)
					{
						try
						{
							sb.AppendLine(_serializer.SerializeMetric(metricsEnumerator.Current));
						}
						catch (DynatraceMetricException e)
						{
							_logger.LogWarning("Skipping metric with the original name '{}'. Serialization failed with message: {}", metric.MetricName, e.Message);
						}
					}
				}
			}
		}
	}
}
