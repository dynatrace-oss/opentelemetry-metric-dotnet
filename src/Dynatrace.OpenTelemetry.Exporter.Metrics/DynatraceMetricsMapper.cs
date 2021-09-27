

using System.Collections.Generic;
using OpenTelemetry.Metrics.Export;
using DynatraceMetric = Dynatrace.MetricUtils.Metric;
using DynatraceMetricFactory = Dynatrace.MetricUtils.MetricsFactory;
using System.Text;

public class DynatraceMetricsMapper
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

	public static IEnumerable<DynatraceMetric> ToDynatraceMetric(Metric metric)
	{
		var metricName = CreateMetricKey(metric);
		foreach (var metricData in metric.Data)
		{
			var timestamp = metricData.Timestamp;
			var dimensions = metricData.Labels;

			switch (metric.AggregationType)
			{
				case AggregationType.DoubleSum:
					{
						var sum = metricData as DoubleSumData;
						var dynatraceMetric = DynatraceMetricFactory.CreateDoubleCounterDelta(metricName: metricName,
																							  value: sum.Sum,
																							  dimensions: dimensions,
																							  timestamp: timestamp);
						yield return dynatraceMetric;
						break;
					}
				case AggregationType.LongSum:
					{
						var sum = metricData as Int64SumData;
						var dynatraceMetric = DynatraceMetricFactory.CreateLongCounterDelta(metricName: metricName,
																							value: sum.Sum,
																							dimensions: dimensions,
																							timestamp: timestamp);
						yield return dynatraceMetric;
						break;
					}
				case AggregationType.DoubleSummary:
					{
						var summary = metricData as DoubleSummaryData;
						var dynatraceMetric = DynatraceMetricFactory.CreateDoubleSummary(metricName: metricName,
																						 min: summary.Min,
																						 max: summary.Max,
																						 sum: summary.Sum,
																						 count: summary.Count,
																						 dimensions: dimensions,
																						 timestamp: timestamp);
						yield return dynatraceMetric;
						break;
					}
				case AggregationType.Int64Summary:
					{
						var summary = metricData as Int64SummaryData;
						var dynatraceMetric = DynatraceMetricFactory.CreateLongSummary(metricName: metricName,
																					   min: summary.Min,
																					   max: summary.Max,
																					   sum: summary.Sum,
																					   count: summary.Count,
																					   dimensions: dimensions,
																					   timestamp: timestamp);
						yield return dynatraceMetric;
						break;
					}
			}
		}
	}
}