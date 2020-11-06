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
using System.Text;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Exporter.Dynatrace.Metrics
{
    public class DynatraceMetricSerializer
    {
        private readonly string prefix;
        private readonly IEnumerable<KeyValuePair<string, string>> tags;

        public DynatraceMetricSerializer(string prefix = null, IEnumerable<KeyValuePair<string, string>> tags = null)
        {
            this.prefix = prefix;
            this.tags = tags ?? Enumerable.Empty<KeyValuePair<string, string>>();
        }

        public string SerializeMetric(Metric metric)
        {
            var sb = new StringBuilder();
            SerializeMetric(sb, metric);
            return sb.ToString();
        }

        public void SerializeMetric(StringBuilder sb, Metric metric)
        {
            foreach (var metricData in metric.Data)
            {
                WriteMetricKey(sb, metric);
                WriteDimensions(sb, metricData.Labels);
                WriteDimensions(sb, tags);
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
                sb.AppendLine();
            }
        }

        private void WriteSummary(StringBuilder sb, double sum, long count, double min, double max)
        {
            sb.Append($" gauge,min={FormatDouble(min)},max={FormatDouble(max)},sum={FormatDouble(sum)},count={count}");
        }

        private string FormatDouble(double min)
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

        private void WriteMetricKey(StringBuilder sb, Metric metric)
        {
            if (!string.IsNullOrEmpty(prefix)) sb.Append($"{prefix}.");
            if (!string.IsNullOrEmpty(metric.MetricNamespace)) sb.Append($"{metric.MetricNamespace}.");
            sb.Append(metric.MetricName);
        }

        private void WriteDimensions(StringBuilder sb, IEnumerable<KeyValuePair<string, string>> labels)
        {
            foreach (var label in labels)
            {
                sb.Append($",{label.Key}={label.Value}");
            }
        }
    }
}
