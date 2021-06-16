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
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Metrics.Export;

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
        private const int _maxBatchSize = 1000;

        public DynatraceMetricsExporter(DynatraceExporterOptions options = null, ILogger<DynatraceMetricsExporter> logger = null)
        {
            this._options = options ?? new DynatraceExporterOptions();
            this._logger = logger ?? NullLogger<DynatraceMetricsExporter>.Instance;
            logger.LogDebug("Dynatrace Metrics Url: {Url}", this._options.Url);
            this._httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(options.ApiToken))
            {
                this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Api-Token", this._options.ApiToken);
            }
            this._httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue("opentelemetry-metric-dotnet")));
            this._serializer = new DynatraceMetricSerializer(this._logger, options.Prefix, options.DefaultDimensions, options.EnrichWithOneAgentMetadata);
        }

        public override async Task<ExportResult> ExportAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, this._options.Url);

            // split all metrics into batches of _maxBatchSize
            var chunked = metrics
            .Select((val, i) => new { val, batch = i / _maxBatchSize })
            .GroupBy(x => x.batch)
            .Select(x => x.Select(v => v.val));

            var exportResults = new List<ExportResult>();

            foreach (var chunk in chunked)
            {
                var sb = new StringBuilder();

                foreach (var metric in chunk)
                {
                    _serializer.SerializeMetric(sb, metric);
                }

                var metricLines = sb.ToString();
                _logger.LogDebug(metricLines);
                httpRequest.Content = new StringContent(metricLines);
                try
                {
                    var response = await this._httpClient.SendAsync(httpRequest);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogDebug("StatusCode: {StatusCode}, Duration: {Duration}ms", response.StatusCode, sw.Elapsed.TotalMilliseconds);
                        exportResults.Add(ExportResult.Success);
                    }
                    else
                    {
                        _logger.LogError("StatusCode: {StatusCode}: Duration: {Duration}ms", response.StatusCode, sw.Elapsed.TotalMilliseconds);
                        _logger.LogError("Content: {Content}", await response.Content.ReadAsStringAsync());
                        exportResults.Add(ExportResult.FailedNotRetryable);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError("Error sending metrics: {Error}", e.Message);
                    throw e;
                }
            }

            // if all chunks were exported successfully, return success, otherwise failed.
            return exportResults.All(x => x == ExportResult.Success) ? ExportResult.Success : ExportResult.FailedNotRetryable;
        }
    }
}
