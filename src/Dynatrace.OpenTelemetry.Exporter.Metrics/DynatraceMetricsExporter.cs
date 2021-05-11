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
        internal readonly DynatraceExporterOptions Options;
        private readonly ILogger<DynatraceMetricsExporter> logger;
        private HttpClient httpClient;
        private DynatraceMetricSerializer serializer;

        public DynatraceMetricsExporter(DynatraceExporterOptions options = null, ILogger<DynatraceMetricsExporter> logger = null)
        {
            this.Options = options ?? new DynatraceExporterOptions();
            this.logger = logger ?? NullLogger<DynatraceMetricsExporter>.Instance;
            logger.LogDebug("Dynatrace Metrics Url: {Url}", options.Url);
            this.httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(options.ApiToken))
            {
                this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Api-Token", this.Options.ApiToken);
            }
            var defaultLabels = new List<KeyValuePair<string, string>>();
            if (options.Tags != null) defaultLabels.AddRange(options.Tags);
            if (options.OneAgentMetadataEnrichment)
            {
                var enricher = new OneAgentMetadataEnricher(this.logger);
                enricher.EnrichWithDynatraceMetadata(defaultLabels);
            }
            this.serializer = new DynatraceMetricSerializer(options.Prefix, defaultLabels);
        }

        public override async Task<ExportResult> ExportAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, this.Options.Url);
            var sb = new StringBuilder();
            foreach (var metric in metrics)
            {
                serializer.SerializeMetric(sb, metric);
            }

            var mintMetrics = sb.ToString();
            logger.LogDebug(mintMetrics);
            httpRequest.Content = new StringContent(mintMetrics);
            try
            {
                var response = await this.httpClient.SendAsync(httpRequest);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogDebug("StatusCode: {StatusCode}, Duration: {Duration}ms", response.StatusCode, sw.Elapsed.TotalMilliseconds);
                }
                else
                {
                    logger.LogError("StatusCode: {StatusCode}: Duration: {Duration}ms", response.StatusCode, sw.Elapsed.TotalMilliseconds);
                    logger.LogError("Content: {Content}", await response.Content.ReadAsStringAsync());
                }
                return ExportResult.Success;
            }
            catch (Exception e)
            {
                logger.LogError("Error sending metrics: {Error}", e.Message);
                throw e;
            }
        }
    }
}
