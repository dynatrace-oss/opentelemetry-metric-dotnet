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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Dynatrace.Metrics;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Exporter.Dynatrace
{
    public class DynatraceMetricsExporter : MetricExporter
    {
        internal readonly DynatraceExporterOptions Options;
        private HttpClient httpClient;
        private DynatraceMetricSerializer serializer;

        public DynatraceMetricsExporter(DynatraceExporterOptions options)
        {
            this.Options = options;
            this.httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(options.ApiToken))
            {
                this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Api-Token", this.Options.ApiToken);
            }
            this.serializer = new DynatraceMetricSerializer(options.Prefix, options.Tags);
        }

        /// <summary>
        /// https://www.dynatrace.com/support/help/how-to-use-dynatrace/metrics/metric-ingestion/metric-ingestion-protocol/
        /// https://www.dynatrace.com/support/help/dynatrace-api/environment-api/metric-v2/post-ingest-metrics
        /// </summary>
        public override async Task<ExportResult> ExportAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            Console.WriteLine("DynatraceMetricsExporter.ExportAsync()");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, this.Options.Url);
            var sb = new StringBuilder();
            foreach (var metric in metrics)
            {
                serializer.SerializeMetric(sb, metric);
            }

            var mintMetrics = sb.ToString();
            Console.WriteLine(mintMetrics);
            httpRequest.Content = new StringContent(mintMetrics);
            try
            {
                var response = await this.httpClient.SendAsync(httpRequest);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine(response.StatusCode);
                }
                else
                {
                    Console.WriteLine(response.StatusCode);
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                }
                return ExportResult.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error sending metrics: {e.Message}");
                throw e;
            }
        }
    }
}
