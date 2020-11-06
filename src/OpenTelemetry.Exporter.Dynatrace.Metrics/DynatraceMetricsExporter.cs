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
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Exporter.Dynatrace
{
    public class DynatraceMetricsExporter : MetricExporter
    {
        internal readonly DynatraceExporterOptions Options;
        private HttpClient httpClient;

        public DynatraceMetricsExporter(DynatraceExporterOptions options)
        {
            this.Options = options;
            this.httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(options.ApiToken))
            {
                this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Api-Token", this.Options.ApiToken);
            }
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
                foreach (var metricData in metric.Data)
                {
                    WriteMetricKey(sb, metric);
                    WriteDimensions(sb, metricData);
                    var labels = metricData.Labels;
                    switch (metric.AggregationType)
                    {
                        case AggregationType.DoubleSum:
                            {
                                var sum = metricData as DoubleSumData;
                                var sumValue = sum.Sum;
                                this.WriteSum(sb, sumValue);
                                break;
                            }

                        case AggregationType.LongSum:
                            {
                                var sum = metricData as Int64SumData;
                                var sumValue = sum.Sum;
                                this.WriteSum(sb, sumValue);
                                break;
                            }

                        case AggregationType.DoubleSummary:
                            {
                                var summary = metricData as DoubleSummaryData;
                                var count = summary.Count;
                                var sum = summary.Sum;
                                var min = summary.Min;
                                var max = summary.Max;
                                this.WriteSummary(sb, sum, count, min, max);
                                break;
                            }

                        case AggregationType.Int64Summary:
                            {
                                var summary = metricData as Int64SummaryData;
                                var count = summary.Count;
                                var sum = summary.Sum;
                                var min = summary.Min;
                                var max = summary.Max;
                                this.WriteSummary(sb, sum, count, min, max);
                                break;
                            }
                    }

                    this.WriteTimestamp(sb, metricData.Timestamp);
                }

                sb.AppendLine();
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

        private void WriteSummary(StringBuilder sb, double sum, long count, double min, double max)
        {
            sb.Append($" gauge,min={FormatDouble(min)},max={FormatDouble(max)},sum={FormatDouble(sum)},count={count}");
        }

        private object FormatDouble(double min)
        {
            return min.ToString("0.############");
        }

        private void WriteTimestamp(StringBuilder sb, DateTime timestamp)
        {
            sb.Append($" {new DateTimeOffset(timestamp.ToUniversalTime()).ToUnixTimeMilliseconds()}");
        }

        private void WriteSum(StringBuilder sb, double sumValue)
        {
            sb.Append($" count,delta={sumValue}");
        }

        private static void WriteMetricKey(StringBuilder sb, Metric metric)
        {
            sb.Append($"{metric.MetricName}");
        }

        private static void WriteDimensions(StringBuilder sb, MetricData data)
        {
            foreach (var label in data.Labels)
            {
                sb.Append($",{label.Key}={label.Value}");
            }
        }
    }
}
