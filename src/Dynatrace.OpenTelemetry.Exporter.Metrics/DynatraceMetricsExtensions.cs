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
			=> DynatraceMetricsFactory.CreateLongCounterDelta(
				metric.Name,
				metricPoint.GetSumLong(),
				metricPoint.GetAttributes(logger),
				metricPoint.EndTime);

		public static DynatraceMetric ToDoubleCounterDelta(this Metric metric, MetricPoint metricPoint, ILogger logger)
			=> DynatraceMetricsFactory.CreateDoubleCounterDelta(
				metric.Name,
				metricPoint.GetSumDouble(),
				metricPoint.GetAttributes(logger),
				metricPoint.EndTime);

		public static DynatraceMetric ToLongGauge(this Metric metric, MetricPoint metricPoint, ILogger logger)
			=> DynatraceMetricsFactory.CreateLongGauge(
				metric.Name,
				metricPoint.GetGaugeLastValueLong(),
				metricPoint.GetAttributes(logger),
				metricPoint.EndTime);

		public static DynatraceMetric ToDoubleGauge(this Metric metric, MetricPoint metricPoint, ILogger logger)
			=> DynatraceMetricsFactory.CreateDoubleGauge(
				metric.Name,
				metricPoint.GetGaugeLastValueDouble(),
				metricPoint.GetAttributes(logger),
				metricPoint.EndTime);

		public static DynatraceMetric ToDoubleHistogram(this Metric metric, MetricPoint metricPoint, ILogger logger)
		{
			var min = GetMinFromBoundaries(metricPoint);
			var max = GetMaxFromBoundaries(metricPoint);

			return DynatraceMetricsFactory.CreateDoubleSummary(
				metric.Name,
				min,
				max,
				metricPoint.GetHistogramSum(),
				metricPoint.GetHistogramCount(),
				metricPoint.GetAttributes(logger),
				metricPoint.EndTime);
		}

		private static double GetMinFromBoundaries(MetricPoint pointData)
		{
			if (pointData.GetHistogramCount() == 1)
			{
				foreach (var item in pointData.GetHistogramBuckets())
				{
					if (item.BucketCount > 0)
					{
						// in case the single bucket contains something, use the mean as min.
						return pointData.GetHistogramSum() / pointData.GetHistogramCount();
					}

					// otherwise the histogram has no data. Use the sum as the min and max, respectively.
					return pointData.GetHistogramSum();
				}
			}

			var i = 0;
			double previousBound = 0;
			foreach (var item in pointData.GetHistogramBuckets())
			{
				if (item.BucketCount > 0)
				{
					// the current bucket contains something.
					if (i == 0)
					{
						// If we are in the first bucket, use the upper bound (which is the lowest specified bound
						// overall) otherwise this would be -Inf, which is not allowed. This is not quite correct,
						// but the best approximation we can get at this point. This might however lead to a min
						// that is bigger than the sum, therefore we return the min of the sum and the lowest
						// bound.
						// Choose the minimum of the following three:
						// - The lowest boundary
						// - The sum (smallest if there are multiple negative measurements smaller than the lowest
						// boundary)
						// - The average in the bucket (smallest if there are multiple positive measurements
						// smaller than the lowest boundary)
						return Math.Min(
							Math.Min(item.ExplicitBound, pointData.GetHistogramSum()),
							pointData.GetHistogramSum() / pointData.GetHistogramCount());
					}
					return previousBound;
				}

				i++;
				previousBound = item.ExplicitBound;
			}

			// there are no counts > 0, so calculating a mean would result in a division by 0. By returning
			// the sum, we can let the backend decide what to do with the value (with a count of 0)
			return pointData.GetHistogramSum();
		}

		private static double GetMaxFromBoundaries(MetricPoint pointData)
		{
			// see getMinFromBoundaries for a very similar method that is annotated.
			if (pointData.GetHistogramCount() == 1)
			{
				foreach (var item in pointData.GetHistogramBuckets())
				{
					if (item.BucketCount > 0)
					{
						// in case the single bucket contains something, use the mean as min.
						return pointData.GetHistogramSum() / pointData.GetHistogramCount();
					}
					return pointData.GetHistogramSum();
				}
			}

			var bucketCounts = new List<long>();
			var bounds = new List<double>();

			foreach (var item in pointData.GetHistogramBuckets())
			{
				bucketCounts.Add(item.BucketCount);
				bounds.Add(item.ExplicitBound);
			}

			var lastElemIdx = bucketCounts.Count - 1;
			// loop over counts in reverse
			for (var i = lastElemIdx; i >= 0; i--)
			{
				if (bucketCounts[i] > 0)
				{
					if (i == lastElemIdx)
					{
						// use the last bound in the bounds array. This can only be the case if there is a count >
						// 0 in the last bucket (lastBound, Inf), therefore, the bound has to be smaller than the
						// actual maximum value, which in turn ensures that the sum is larger than the bound we
						// use as max here.
						return bounds[i - 1];
					}
					// in any bucket except the last, make sure the sum is greater than or equal to the max,
					// otherwise report the sum.
					return Math.Min(bounds[i], pointData.GetHistogramSum());
				}
			}

			return pointData.GetHistogramSum();
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

