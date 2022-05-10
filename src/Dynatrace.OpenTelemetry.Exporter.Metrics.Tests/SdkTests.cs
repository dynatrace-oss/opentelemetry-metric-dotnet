using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dynatrace.OpenTelemetry.Exporter.Metrics.Tests.Utils;
using Moq;
using Moq.Protected;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Xunit;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests
{
	public class SdkTests
	{
		private readonly TagList _attributes = new() { { "attr1", "v1" }, { "attr2", "v2" } };

		[Fact]
		public void Export_View_CounterWithDeltaTemporality()
		{
			// Arrange
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			var exportedMetrics = new List<Metric>();

			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddInMemoryExporter(exportedMetrics,
					options => options.TemporalityPreference = MetricReaderTemporalityPreference.Delta)
				.AddView(instrumentName: "counter", name: "myview")
				.Build();

			var counter = meter.CreateCounter<long>("counter");

			counter.Add(10, _attributes);
			provider.ForceFlush();
			var point = ToEnumerable(exportedMetrics.First().GetMetricPoints().GetEnumerator()).First();
			Assert.Equal(10, point.GetSumLong());

			counter.Add(20, _attributes);
			provider.ForceFlush();
			point = ToEnumerable(exportedMetrics.First().GetMetricPoints().GetEnumerator()).First();
			Assert.Equal(20, point.GetSumLong());

			counter.Add(30, _attributes);
			provider.ForceFlush();
			point = ToEnumerable(exportedMetrics.First().GetMetricPoints().GetEnumerator()).First();
			Assert.Equal(30, point.GetSumLong());
		}

		private static IEnumerable<MetricPoint> ToEnumerable(MetricPointsAccessor.Enumerator enumerator)
		{
			while (enumerator.MoveNext())
			{
				yield return enumerator.Current;
			}
		}
	}
}
