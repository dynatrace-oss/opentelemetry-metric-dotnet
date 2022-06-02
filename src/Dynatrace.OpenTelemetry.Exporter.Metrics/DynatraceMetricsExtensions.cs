// <copyright company="Dynatrace LLC">
// Copyright 2021 Dynatrace LLC
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
using Dynatrace.MetricUtils;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics
{
	internal static class DynatraceMetricsExtensions
	{
		public static DynatraceMetric ToLongCounterDelta(this Metric metric, MetricPoint metricPoint, ILogger logger)
		{
			return DynatraceMetricsFactory.CreateLongCounterDelta(
				metric.Name,
				metricPoint.GetSumLong(),
				metricPoint.GetAttributes(logger),
				metricPoint.EndTime);
		}

		public static DynatraceMetric ToDoubleCounterDelta(this Metric metric, MetricPoint metricPoint, ILogger logger)
		{
			return DynatraceMetricsFactory.CreateDoubleCounterDelta(
				metric.Name,
				metricPoint.GetSumDouble(),
				metricPoint.GetAttributes(logger),
				metricPoint.EndTime);
		}

		public static DynatraceMetric ToLongGauge(this Metric metric, MetricPoint metricPoint, ILogger logger)
		{
			return DynatraceMetricsFactory.CreateLongGauge(
				metric.Name,
				metricPoint.GetGaugeLastValueLong(),
				metricPoint.GetAttributes(logger),
				metricPoint.EndTime);
		}

		public static DynatraceMetric ToDoubleGauge(this Metric metric, MetricPoint metricPoint, ILogger logger)
		{
			return DynatraceMetricsFactory.CreateDoubleGauge(
				metric.Name,
				metricPoint.GetGaugeLastValueDouble(),
				metricPoint.GetAttributes(logger),
				metricPoint.EndTime);
		}

		public static DynatraceMetric ToDoubleHistogram(this Metric metric, MetricPoint metricPoint, ILogger logger)
		{
			var buckets = GetHistogramBuckets(metricPoint);
			var min = GetMinFromBoundaries(metricPoint.GetHistogramSum(), metricPoint.GetHistogramCount(), buckets);
			var max = GetMaxFromBoundaries(metricPoint.GetHistogramSum(), metricPoint.GetHistogramCount(), buckets);

			return DynatraceMetricsFactory.CreateDoubleSummary(
				metric.Name,
				min,
				max,
				metricPoint.GetHistogramSum(),
				metricPoint.GetHistogramCount(),
				metricPoint.GetAttributes(logger),
				metricPoint.EndTime);
		}

		private static List<HistogramBucket> GetHistogramBuckets(MetricPoint pointData)
		{
			var buckets = new List<HistogramBucket>();
			foreach (var bucket in pointData.GetHistogramBuckets())
			{
				buckets.Add(bucket);
			}

			return buckets;
		}

		private static double GetMinFromBoundaries(double histogramSum, long histogramValueCount, IReadOnlyList<HistogramBucket> buckets)
		{
			if (buckets.Count == 0)
			{
				if (histogramValueCount > 1)
				{
					// in case the single bucket contains something, use the mean as min.
					return histogramSum / histogramValueCount;
				}

				// otherwise the histogram has no data. Use the sum as the min and max, respectively.
				return histogramSum;
			}

			for (var i = 0; i < buckets.Count; i++)
			{
				if (buckets[i].BucketCount > 0)
				{
					// the current bucket contains something.
					if (i == 0)
					{
						// In the first bucket, (-Inf, firstBound], use firstBound (this is the lowest specified
						// bound overall). This is not quite correct but the best approximation we can get at this
						// point. However, this might lead to a min bigger than the mean, thus choose the minimum
						// of the following:
						// - The lowest boundary
						// - The average of the histogram (histogram sum / sum of counts)
						return Math.Min(
							Math.Min(buckets[i].ExplicitBound, histogramSum),
							histogramSum / histogramValueCount);
					}

					return buckets[i - 1].ExplicitBound;
				}
			}

			// there are no counts > 0, so calculating a mean would result in a division by 0. By returning
			// the sum, we can let the backend decide what to do with the value (with a count of 0)
			return histogramSum;
		}

		private static double GetMaxFromBoundaries(double histogramSum, long histogramValueCount, IReadOnlyList<HistogramBucket> buckets)
		{
			if (buckets.Count == 0)
			{
				if (histogramValueCount > 1)
				{
					// in case the single bucket contains something, use the mean as max.
					return histogramSum / histogramValueCount;
				}

				// otherwise the histogram has no data. Use the sum as the min and max, respectively.
				return histogramSum;
			}

			var lastElemIdx = buckets.Count - 1;
			// loop over counts in reverse
			for (var i = lastElemIdx; i >= 0; i--)
			{
				if (buckets[i].BucketCount > 0)
				{
					if (i == lastElemIdx)
					{
						// use the last bound in the buckets array. This can only be the case if there is a count >
						// 0 in the last bucket (lastBound, Inf). In some cases, the mean of the histogram is
						// larger than this bound, thus use the maximum of the estimated bound and the mean.
						return Math.Max(buckets[i - 1].ExplicitBound, histogramSum / histogramValueCount);
					}

					// In any other bucket (lowerBound, upperBound], use the upperBound.
					return buckets[i].ExplicitBound;
				}
			}

			return histogramSum;
		}

		private static IEnumerable<KeyValuePair<string, string>> GetAttributes(this MetricPoint metricPoint, ILogger logger)
		{
			foreach (var tag in metricPoint.Tags)
			{
				if (!(tag.Value is string))
				{
					logger.UnsupportedMetricType(tag.Value.GetType().Name);
				}
				else
				{
					yield return new KeyValuePair<string, string>(tag.Key, tag.Value.ToString());
				}
			}
		}
	}
}
