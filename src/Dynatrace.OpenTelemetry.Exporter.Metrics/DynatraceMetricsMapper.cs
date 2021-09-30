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

using System.Collections.Generic;
using OpenTelemetry.Metrics.Export;
using DynatraceMetric = Dynatrace.MetricUtils.Metric;
using DynatraceMetricFactory = Dynatrace.MetricUtils.MetricsFactory;
using DynatraceMetricException = Dynatrace.MetricUtils.MetricException;
using System.Text;
using Microsoft.Extensions.Logging;

internal static class DynatraceMetricsMapper
{

	/// <summary>
	/// Combines metric namespace and key into a single key for use in <see cref="Dynatrace.MetricUtils.Metric">.
	/// </summary>
	/// <returns>A metric key in the form {MetricNamespace}.{MetricKey}</returns>
	private static string CreateMetricKey(Metric metric)
	{
		var keyBuilder = new StringBuilder();
		if (!string.IsNullOrEmpty(metric.MetricNamespace))
		{
			keyBuilder.Append($"{metric.MetricNamespace}.");
		}
		keyBuilder.Append(metric.MetricName);
		return keyBuilder.ToString();
	}

	internal static IEnumerable<DynatraceMetric> ToDynatraceMetric(Metric metric, ILogger logger)
	{
		var metricName = CreateMetricKey(metric);
		foreach (var metricData in metric.Data)
		{
			DynatraceMetric dynatraceMetric = null;
			var timestamp = metricData.Timestamp;
			var dimensions = metricData.Labels;
			try
			{
				switch (metric.AggregationType)
				{
					case AggregationType.DoubleSum:
						{
							var sum = metricData as DoubleSumData;
							dynatraceMetric = DynatraceMetricFactory.CreateDoubleCounterDelta(
								metricName: metricName,
								value: sum.Sum,
								dimensions: dimensions,
								timestamp: timestamp);
							break;
						}
					case AggregationType.LongSum:
						{
							var sum = metricData as Int64SumData;
							dynatraceMetric = DynatraceMetricFactory.CreateLongCounterDelta(
								metricName: metricName,
								value: sum.Sum,
								dimensions: dimensions,
								timestamp: timestamp);
							break;
						}
					case AggregationType.DoubleSummary:
						{
							var summary = metricData as DoubleSummaryData;
							dynatraceMetric = DynatraceMetricFactory.CreateDoubleSummary(
								metricName: metricName,
								min: summary.Min,
								max: summary.Max,
								sum: summary.Sum,
								count: summary.Count,
								dimensions: dimensions,
								timestamp: timestamp);
							break;
						}
					case AggregationType.Int64Summary:
						{
							var summary = metricData as Int64SummaryData;
							dynatraceMetric = DynatraceMetricFactory.CreateLongSummary(
								metricName: metricName,
								min: summary.Min,
								max: summary.Max,
								sum: summary.Sum,
								count: summary.Count,
								dimensions: dimensions,
								timestamp: timestamp);
							break;
						}
				}
			}
			catch (DynatraceMetricException e)
			{
				logger.LogWarning("Skipping metric with the original name '{}'. Mapping failed with message: {}", metric.MetricName, e.Message);
			}

			if(dynatraceMetric != null)
			{
				yield return dynatraceMetric;
			}
		}
	}
}
