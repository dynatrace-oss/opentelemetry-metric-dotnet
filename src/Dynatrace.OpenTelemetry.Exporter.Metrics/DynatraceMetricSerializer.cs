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
using Dynatrace.OpenTelemetry.Exporter.Metrics.Utils;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics.Export;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics
{
    public class DynatraceMetricSerializer
    {
        private readonly ILogger<DynatraceMetricsExporter> _logger;
        private readonly string _prefix;
        private readonly IEnumerable<KeyValuePair<string, string>> _defaultDimensions;
        private readonly IEnumerable<KeyValuePair<string, string>> _oneAgentDimensions;
        private static readonly IEnumerable<KeyValuePair<string, string>> _staticDimensions = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("dt.metrics.source", "opentelemetry") };
        private static readonly int MaxDimensions = 50;

        public DynatraceMetricSerializer(ILogger<DynatraceMetricsExporter> logger, string prefix = null, IEnumerable<KeyValuePair<string, string>> defaultDimensions = null, bool enrichWithDynatraceMetadata = true)
        {
            this._logger = logger;
            this._prefix = prefix;
            this._defaultDimensions = defaultDimensions ?? Enumerable.Empty<KeyValuePair<string, string>>();
            if (enrichWithDynatraceMetadata)
            {
                var enricher = new OneAgentMetadataEnricher(this._logger);
                _oneAgentDimensions = enricher.GetOneAgentDimensions();
            }
            else
            {
                _oneAgentDimensions = Enumerable.Empty<KeyValuePair<string, string>>();
            }
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
                var metricKey = CreateMetricKey(metric);
                // skip lines with invalid metric keys.
                if (string.IsNullOrEmpty(metricKey)) {
                    _logger.LogWarning("metric key was empty after normalization, skipping metric (original name {})", metric.MetricName);
                    continue;
                }
                sb.Append(metricKey);

                var normalizedDimensions = DeduplicateAndNormalizeDimensions(this._defaultDimensions, metricData.Labels, this._oneAgentDimensions, DynatraceMetricSerializer._staticDimensions);
                WriteDimensions(sb, normalizedDimensions);

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
            sb.Append($" {new DateTimeOffset(timestamp).ToUniversalTime().ToUnixTimeMilliseconds()}");
        }

        private void WriteSum(StringBuilder sb, double sumValue)
        {
            sb.Append($" count,delta={sumValue}");
        }

        private string CreateMetricKey(Metric metric) {
            var keyBuilder = new StringBuilder();
            if (!string.IsNullOrEmpty(_prefix)) keyBuilder.Append($"{_prefix}.");
            // todo is this needed?
            if (!string.IsNullOrEmpty(metric.MetricNamespace)) keyBuilder.Append($"{metric.MetricNamespace}.");
            keyBuilder.Append(metric.MetricName);
            return ToMetricKey(keyBuilder.ToString());
        }

        private void WriteMetricKey(StringBuilder sb, Metric metric)
        {
            var keyBuilder = new StringBuilder();
            if (!string.IsNullOrEmpty(_prefix)) keyBuilder.Append($"{_prefix}.");
            // if (!string.IsNullOrEmpty(metric.MetricNamespace)) keyBuilder.Append($"{metric.MetricNamespace}.");
            keyBuilder.Append(metric.MetricName);
            sb.Append(ToMetricKey(keyBuilder.ToString()));
        }


        // further right overwrites further left.
        internal static IEnumerable<KeyValuePair<string, string>> DeduplicateAndNormalizeDimensions(params IEnumerable<KeyValuePair<string, string>>[] dimensionLists)
        {
            var dictionary = new Dictionary<string, string>();

            foreach (var dimensionList in dimensionLists)
            {
                foreach (var dimension in dimensionList)
                {
                    var normalizedKey = ToDimensionKey(dimension.Key);
                    if (!string.IsNullOrEmpty(normalizedKey))
                    {
                        dictionary.Add(normalizedKey, ToDimensionValue(dimension.Value));
                    }
                }
            }

            return dictionary.ToList();
        }

        private void WriteDimensions(StringBuilder sb, IEnumerable<KeyValuePair<string, string>> labels)
        {
            foreach (var label in labels.Take(MaxDimensions))
            {
                // todo make sure empty keys are handled.
                sb.Append($",{ToDimensionKey(label.Key)}={ToDimensionValue(label.Value)}");
            }
        }

        /// <summary>
        /// Transforms OpenTelemetry metric names to metric keys valid in Dynatrace
        /// </summary>
        /// <returns>a valid metric key or null, if the input could not be normalized</returns>
        private static string ToMetricKey(string input)
        {
            return Normalize.MetricKey(input);
        }

        /// <summary>
        /// Transforms OpenTelemetry label values to metric values valid in Dynatrace.
        /// </summary>
        /// <returns>a valid dimension key or null, if the input could not be normalized</returns>
        private static string ToDimensionKey(string input)
        {
            return Normalize.DimensionKey(input);
        }

        private static string ToDimensionValue(string input)
        {
            return Normalize.EscapeDimensionValue(Normalize.DimensionValue(input));
        }
    }
}
