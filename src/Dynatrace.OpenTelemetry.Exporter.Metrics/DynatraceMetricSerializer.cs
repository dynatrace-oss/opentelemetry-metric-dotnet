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
        private readonly IEnumerable<KeyValuePair<string, string>> _staticDimensions;
        private static readonly int MaxDimensions = 50;

        // public constructor.
        public DynatraceMetricSerializer(ILogger<DynatraceMetricsExporter> logger, string prefix = null, IEnumerable<KeyValuePair<string, string>> defaultDimensions = null, bool enrichWithOneAgentMetadata = true)
        : this(logger, prefix, defaultDimensions, PrepareOneAgentDimensions(logger, enrichWithOneAgentMetadata)) { }

        // this is required to read the OneAgent dimensions and still use constructor chaining
        private static IEnumerable<KeyValuePair<string, string>> PrepareOneAgentDimensions(ILogger<DynatraceMetricsExporter> logger, bool enrichWithDynatraceMetadata = true)
        {
            var oneAgentDimensions = new List<KeyValuePair<string, string>> { };

            if (enrichWithDynatraceMetadata)
            {
                var enricher = new OneAgentMetadataEnricher(logger);
                var dimensions = new List<KeyValuePair<string, string>>();
                enricher.EnrichWithDynatraceMetadata(oneAgentDimensions);
            }
            return oneAgentDimensions;
        }

        // internal constructor offers an interface for testing and is used by the public constructor
        internal DynatraceMetricSerializer(ILogger<DynatraceMetricsExporter> logger, string prefix, IEnumerable<KeyValuePair<string, string>> defaultDimensions, IEnumerable<KeyValuePair<string, string>> oneAgentDimensions)
        {
            this._logger = logger;
            this._prefix = prefix;
            this._defaultDimensions = Normalize.DimensionList(defaultDimensions) ?? Enumerable.Empty<KeyValuePair<string, string>>();

            var staticDimensions = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("dt.metrics.source", "opentelemetry") };
            staticDimensions.AddRange(oneAgentDimensions);
            this._staticDimensions = Normalize.DimensionList(staticDimensions);
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
                if (string.IsNullOrEmpty(metricKey))
                {
                    _logger.LogWarning("metric key was empty after normalization, skipping metric (original name {})", metric.MetricName);
                    continue;
                }
                sb.Append(metricKey);

                // default dimensions and static dimensions are normalized once upon serializer creation.
                // the labels from opentelemetry are normalized here, then all dimensions are merged.
                var normalizedDimensions = MergeDimensions(this._defaultDimensions, Normalize.DimensionList(metricData.Labels), this._staticDimensions);

                // merged dimensions are normalized and escaped since we called Normalize.DimensionList on each of the sublists.
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
            sb.Append($" {new DateTimeOffset(timestamp.ToLocalTime()).ToUnixTimeMilliseconds()}");
        }

        private void WriteSum(StringBuilder sb, double sumValue)
        {
            sb.Append($" count,delta={sumValue}");
        }

        /// <summary>
        /// Transforms OpenTelemetry metric names to metric keys valid in Dynatrace
        /// </summary>
        /// <returns>a valid metric key or null, if the input could not be normalized</returns>
        private string CreateMetricKey(Metric metric)
        {
            var keyBuilder = new StringBuilder();
            if (!string.IsNullOrEmpty(_prefix)) keyBuilder.Append($"{_prefix}.");
            // todo is this needed?
            if (!string.IsNullOrEmpty(metric.MetricNamespace)) keyBuilder.Append($"{metric.MetricNamespace}.");
            keyBuilder.Append(metric.MetricName);
            return Normalize.MetricKey(keyBuilder.ToString());
        }

        // Items from Enumerables passed further right overwrite items from Enumerables passed further left.
        // Pass only normalized dimension lists to this function.
        internal static List<KeyValuePair<string, string>> MergeDimensions(params IEnumerable<KeyValuePair<string, string>>[] dimensionLists)
        {
            var dictionary = new Dictionary<string, string>();

            if (dimensionLists == null)
            {
                return new List<KeyValuePair<string, string>>();
            }

            foreach (var dimensionList in dimensionLists)
            {
                if (dimensionList != null)
                {
                    foreach (var dimension in dimensionList)
                    {
                        if (!dictionary.ContainsKey(dimension.Key))
                        {
                            dictionary.Add(dimension.Key, dimension.Value);
                        }
                        else
                        {
                            dictionary[dimension.Key] = dimension.Value;
                        }
                    }
                }
            }

            return dictionary.ToList();
        }

        // pass only normalized lists to this function.
        private void WriteDimensions(StringBuilder sb, List<KeyValuePair<string, string>> dimensions)
        {
            // should be negative if there are fewer dimensions than the maximum
            var diffToMaxDimensions = MaxDimensions - dimensions.Count;
            var toSkip = diffToMaxDimensions < 0 ? Math.Abs(diffToMaxDimensions) : 0;

            // if there are more dimensions, skip the first n dimensions so that 50 dimensions remain
            foreach (var dimension in dimensions.Skip(toSkip))
            {
                sb.Append($",{dimension.Key}={dimension.Value}");
            }
        }
    }
}
