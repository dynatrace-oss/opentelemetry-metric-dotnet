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
using System.Text.RegularExpressions;
using OpenTelemetry.Metrics.Export;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics
{
    public class DynatraceMetricSerializer
    {
        private readonly string _prefix;
        private readonly IEnumerable<KeyValuePair<string, string>> _dimensions;

        private const int MaxLengthMetricKey = 250;
        private const int MaxLengthDimensionKey = 100;
        private const int MaxLengthDimensionValue = 250;
        private const int MaxDimensions = 50;

        public DynatraceMetricSerializer(string prefix = null, IEnumerable<KeyValuePair<string, string>> tags = null)
        {
            this._prefix = prefix;
            this._dimensions = tags ?? Enumerable.Empty<KeyValuePair<string, string>>();
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
                WriteDimensions(sb, this._dimensions);
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

        private void WriteMetricKey(StringBuilder sb, Metric metric)
        {
            var keyBuilder = new StringBuilder();
            if (!string.IsNullOrEmpty(_prefix)) keyBuilder.Append($"{_prefix}.");
            if (!string.IsNullOrEmpty(metric.MetricNamespace)) keyBuilder.Append($"{metric.MetricNamespace}.");
            keyBuilder.Append(metric.MetricName);
            sb.Append(ToMintMetricKey(keyBuilder.ToString()));
        }

        private void WriteDimensions(StringBuilder sb, IEnumerable<KeyValuePair<string, string>> labels)
        {
            foreach (var label in labels.Take(MaxDimensions))
            {
                sb.Append($",{ToMintDimensionKey(label.Key)}={ToMintDimensionValue(label.Value)}");
            }
        }

        /// <summary>
        /// Transforms OpenTelemetry metric names according to the MINT protocol
        /// </summary>
        /// <returns>a valid MINT metric key or null, if the input could not be normalized</returns>
        internal static string ToMintMetricKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }
            if (input.Length > MaxLengthMetricKey)
            {
                input = input.Substring(0, MaxLengthMetricKey);
            }
            return ReplaceKeyCharacters(TrimKey(RemoveInvalidKeySections(input)));
        }

        /// <summary>
        /// Transforms OpenTelemetry label keys according to the MINT protocol
        /// </summary>
        /// <returns>a valid MINT dimension key or null, if the input could not be normalized</returns>
        internal static string ToMintDimensionKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }
            if (input.Length > MaxLengthDimensionKey)
            {
                input = input.Substring(0, MaxLengthDimensionKey);
            }
            return ReplaceKeyCharacters(TrimKey(RemoveInvalidKeySections(input)).ToLower());
        }

        /// <summary>
        /// Removes leading or trailing characters invalid for keys
        /// </summary>
        private static string TrimKey(string str)
        {
            str = Regex.Replace(str, @"^[^a-zA-Z][^a-zA-Z_]*", "");
            return Regex.Replace(str, @"[^a-zA-Z_0-9]*$", "");
        }

        /// <summary>
        /// Replaces characters invalid for keys with underscores
        /// </summary>
        private static string ReplaceKeyCharacters(string str)
        {
            return Regex.Replace(str, @"[^a-zA-Z0-9:_\-\.]+", "_");
        }

        /// <summary>
        /// Removes invalid (including empty) key sections
        /// </summary>
        private static string RemoveInvalidKeySections(string str)
        {
            return Regex.Replace(str, @"\.+[^a-zA-Z][^a-zA-Z0-9:_\-\.]*", ".");
        }

        /// <summary>
        /// Transforms OpenTelemetry label values according to the MINT protocol
        /// </summary>
        /// <returns>a valid MINT dimension value or null, if the input could not be normalized</returns>
        internal static string ToMintDimensionValue(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }
            if (input.Length > MaxLengthDimensionValue)
            {
                input = input.Substring(0, MaxLengthDimensionValue);
            }
            input = Regex.Replace(input, @"([,= \\])", "\\$1");
            return Regex.Replace(input, @"[^a-zA-Z0-9:_\-\.,= \\]", "_");
        }
    }
}
