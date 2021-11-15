using System;
using System.Collections.Generic;
using OpenTelemetry.Metrics;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests
{
	/// <summary>
	/// Handy type to extract <see cref="MetricPoint"/>s from a <see cref="Metric"/>
	/// </summary>
	internal record MetricTest(DateTimeOffset EndTime)
	{
		internal long TimeStamp => EndTime.ToUnixTimeMilliseconds();

		internal static IEnumerable<MetricTest> FromMetricPoints(BatchMetricPoint metricPoints)
		{
			foreach (var point in metricPoints)
			{
				yield return new MetricTest(point.EndTime);
			}
		}
	}
}
