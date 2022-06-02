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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dynatrace.OpenTelemetry.Exporter.Metrics.Tests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Xunit;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests
{
	public sealed class DynatraceMetricsExporterTests : IDisposable
	{
		private readonly TagList _attributes = new() { { "attr1", "v1" }, { "attr2", "v2" } };
		private readonly MeterProvider _meterProvider;
		private readonly List<Metric> _exportedMetrics;
		private readonly Meter _meter;

		public DynatraceMetricsExporterTests()
		{
			_meter = new Meter(Guid.NewGuid().ToString(), "0.0.1");
			_exportedMetrics = new List<Metric>();

			_meterProvider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(_meter.Name)
				.AddInMemoryExporter(_exportedMetrics, options => options.TemporalityPreference = MetricReaderTemporalityPreference.Delta)
				.Build();
		}

		public void Dispose()
		{
			_meter.Dispose();
			_meterProvider.Dispose();
		}

		[Fact]
		public async Task Export_WithDefaultOptions_ShouldSendRequestToOneAgent()
		{
			// Arrange
			var counter = _meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);
			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			// Act
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			var point = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();

			var expected = $"counter,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry count,delta=10 {point.TimeStamp}";
			var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
			Assert.Equal(expected, actualMetricString);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Fact]
		public void Export_ReceivedErrorFromServer_ReturnsFailedExportResult()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(
				r => actualRequestMessage = r,
				HttpStatusCode.InternalServerError);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act
			_meterProvider.ForceFlush();
			var exportResult = sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			Assert.Equal(ExportResult.Failure, exportResult);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Fact]
		public void Export_ExceptionWithRequest_ReturnsFailedExportResult()
		{
			// Arrange
			var mockMessageHandler = new Mock<HttpMessageHandler>();
			mockMessageHandler.Protected()
				.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
					ItExpr.IsAny<CancellationToken>())
				.Throws(new HttpRequestException());

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act
			_meterProvider.ForceFlush();
			var exportResult = sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			Assert.Equal(ExportResult.Failure, exportResult);
		}

		[Fact]
		public async Task Export_WithUriAndTokenOptions_ShouldSendRequestToUrlWithToken()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(
				new DynatraceExporterOptions { Url = "http://my.url", ApiToken = "test-token" }, null,
				new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			var point = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();

			var expected = $"counter,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry count,delta=10 {point.TimeStamp}";
			var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
			Assert.Equal(expected, actualMetricString);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage, endpoint: "http://my.url/", apiToken: "test-token");
		}

		[Fact]
		public async Task Export_WithPrefixOptions_ShouldAppendPrefixToMetrics()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(
				new DynatraceExporterOptions { Prefix = "my.prefix" }, null,
				new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			var point = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();

			var expected = $"my.prefix.counter,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry count,delta=10 {point.TimeStamp}";
			var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
			Assert.Equal(expected, actualMetricString);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Fact]
		public async Task Export_WithDefaultDimensions_ShouldAddDimensionsToMetrics()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var defaultDimensions = new KeyValuePair<string, string>[] { new("d1", "v1"), new("d2", "v2"), };

			var sut = new DynatraceMetricsExporter(
				new DynatraceExporterOptions { DefaultDimensions = defaultDimensions }, null,
				new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			var point = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();

			var expected = $"counter,d1=v1,d2=v2,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry count,delta=10 {point.TimeStamp}";
			var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
			Assert.Equal(expected, actualMetricString);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Fact]
		public void Export_WithTooLargeMetric_ShouldNotSendRequest()
		{
			// Arrange
			var mockMessageHandler = SetupHttpMock();
			var mockLogger = new Mock<ILogger<DynatraceMetricsExporter>>();
			mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true);

			// 20 dimensions of ~ 100 characters should result in lines with more than 2000 characters
			var dimensions = new TagList();
			for (var i = 0; i < 20; i++)
			{
				// creates a dimension that takes up a little more than 100 characters
				dimensions.Add(new(new string('a', 50) + i, new string('b', 50) + i));
			}

			var sut = new DynatraceMetricsExporter(null, mockLogger.Object, new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<int>("counter");
			counter.Add(10, dimensions.ToArray());

			// Act
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Never(),
				ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
				ItExpr.IsAny<CancellationToken>());

			mockLogger.Verify(x => x.Log(
					LogLevel.Warning,
					5, // eventid define in LoggerExtensions
					It.Is<It.IsAnyType>((value, type) => value.ToString()!.Contains("Skipping metric with the original name")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception, string>>()),
				Times.Exactly(1));
		}

		[Fact]
		public async Task Export_SimulateMultipleExports_ShouldExportCorrectDelta()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<int>("counter");

			for (var i = 10; i <= 30; i += 10)
			{
				counter.Add(i, _attributes);

				_exportedMetrics.Clear();
				_meterProvider.ForceFlush();
				sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

				// Assert
				var point = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();

				var expected = $"counter,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry count,delta={i} {point.TimeStamp}";
				var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
				Assert.Equal(expected, actualMetricString);

				AssertExportRequest(actualRequestMessage);
			}

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(3),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());
		}

		[Fact]
		public async Task Export_MultipleMetricStreams_ShouldExportMultipleLines()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var counterA = _meter.CreateCounter<int>("counterA");
			counterA.Add(10, _attributes);

			var counterB = _meter.CreateCounter<int>("counterB");
			counterB.Add(20, _attributes);

			// Act
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			Assert.Equal(2, _exportedMetrics.Count);
			var pointA = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();
			var pointB = MetricTest.FromMetricPoints(_exportedMetrics.Last().GetMetricPoints()).First();

			var expected = @$"counterA,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry count,delta=10 {pointA.TimeStamp}
counterB,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry count,delta=20 {pointB.TimeStamp}";

			var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
			Assert.Equal(expected, actualMetricString);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Fact]
		public async Task Export_LongSum_ShouldExportAsDelta()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<long>("counter");
			counter.Add(10, _attributes);

			// Act
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			var point = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();

			var expected = $"counter,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry count,delta=10 {point.TimeStamp}";
			var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
			Assert.Equal(expected, actualMetricString);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Fact]
		public async Task Export_DoubleSum_ShouldExportAsDelta()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<double>("double_counter");
			counter.Add(10.3, _attributes);

			// Act
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			var point = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();

			var expected = $"double_counter,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry count,delta=10.3 {point.TimeStamp}";
			var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
			Assert.Equal(expected, actualMetricString);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Fact]
		public async Task Export_ObservableLongCounter_ShouldExportAsDelta()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			// When exported twice, this obs counter will output
			// 10 -> 20. Since we expect deltas to be calculated by the SDK
			// we assert both exports to be 10
			var i = 1;
			_meter.CreateObservableCounter("obs_counter",
				() => new List<Measurement<long>> { new(i++ * 10, _attributes) });

			// Perform two exports to ensure deltas are exported correctly
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));
			await AssertLines(10);
			_exportedMetrics.Clear();

			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));
			await AssertLines(10);

			async Task AssertLines(long expectedValue)
			{
				var point = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();

				var expected = $"obs_counter,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry count,delta={expectedValue} {point.TimeStamp}";
				var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
				Assert.Equal(expected, actualMetricString);
			}

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(2),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Fact]
		public async Task Export_ObservableDoubleCounter_ShouldExportAsDelta()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			// When exported twice, this obs counter will output
			// 10.3 -> 20.6. Since we expect deltas to be calculated by the SDK
			// we assert both exports to be 10.3
			var i = 1;
			_meter.CreateObservableCounter("double_obs_counter",
				() => new List<Measurement<double>> { new(i++ * 10.3, _attributes) });

			// Perform two exports to ensure deltas are exported correctly
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));
			await AssertLines(10.3);
			_exportedMetrics.Clear();

			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));
			await AssertLines(10.3);

			async Task AssertLines(double expectedValue)
			{
				var point = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();

				var expected = $"double_obs_counter,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry count,delta={expectedValue} {point.TimeStamp}";
				var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
				Assert.Equal(expected, actualMetricString);
			}

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(2),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Fact]
		public async Task Export_ObservableLongGauge()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			_meter.CreateObservableGauge("gauge",
				() => new List<Measurement<long>> { new(10, _attributes) });

			// Act
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			var point = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();

			var expected = $"gauge,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry gauge,10 {point.TimeStamp}";
			var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
			Assert.Equal(expected, actualMetricString);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Fact]
		public async Task Export_ObservableDoubleGauge()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			_meter.CreateObservableGauge("double_gauge",
				() => new List<Measurement<double>> { new(10.3, _attributes), });

			// Act
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			var point = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();

			var expected = $"double_gauge,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry gauge,10.3 {point.TimeStamp}";
			var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
			Assert.Equal(expected, actualMetricString);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Fact]
		public async Task Export_LongHistogram()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			// default bounds are 0, 5, 10, 25, 50, 75, 100, 250, 500
			var histogram = _meter.CreateHistogram<long>("histogram");

			histogram.Record(1, _attributes);
			histogram.Record(6, _attributes);
			histogram.Record(11, _attributes);
			histogram.Record(21, _attributes);

			// Act
			_meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			var point = MetricTest.FromMetricPoints(_exportedMetrics.First().GetMetricPoints()).First();

			var expected = $"histogram,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry gauge,min=0,max=25,sum=39,count=4 {point.TimeStamp}";
			var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
			Assert.Equal(expected, actualMetricString);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Fact]
		public async Task Export_Histogram_CustomBounds()
		{
			// Arrange
			using var meter = new Meter(Guid.NewGuid().ToString(), "0.0.1");
			var exportedMetrics = new List<Metric>();

			using var meterProvider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddInMemoryExporter(exportedMetrics,
					options => options.TemporalityPreference = MetricReaderTemporalityPreference.Delta)
				.AddView(
					instrumentName: "histogram",
					new ExplicitBucketHistogramConfiguration { Boundaries = new double[] { 0.1, 1.2, 3.4, 5.6 } })
				.Build();

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var histogram = meter.CreateHistogram<double>("histogram");

			histogram.Record(0.2, _attributes);
			histogram.Record(1.4, _attributes);
			histogram.Record(2, _attributes);
			histogram.Record(4, _attributes);

			// Act
			meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(exportedMetrics.ToArray(), exportedMetrics.Count));

			// Assert
			var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

			var expected = $"histogram,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry gauge,min=0.1,max=5.6,sum=7.6,count=4 {point.TimeStamp}";
			var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
			Assert.Equal(expected, actualMetricString);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		[Theory]
		// min: Values between the first two boundaries.
		[InlineData(new[] { 1d, 2d, 3d, 4d, 5d }, 6, new[] { 1.5, 3.2, 3.2, 3.5, 4.8, 4.9 },  21.1, 1d, 5d)]
		// min: First bucket has value, use the first boundary as estimation instead of Inf.
		[InlineData(new[] { 1d, 2d, 3d, 4d, 5d }, 8, new[] { 0.5, 3.1, 3.1, 3.1, 6, 6, 6, 6.7 }, 34.5, 1d, 5d)]
		// min: Only the first bucket has values, use the mean (0.25) Otherwise, the min would be estimated as 1, and min <= avg would be violated.
		[InlineData(new[] { 1d, 2d, 3d, 4d, 5d }, 3, new[] { 0.3, 0.3, 0.15 }, 0.75, 0.25, 1)]
		// min: Just one bucket from -Inf to Inf, calculate the mean as min value.
		[InlineData(new double[0], 4, new[] { 2, 2, 2, 2.8 }, 8.8, 2.2, 2.2)]
		// min: Just one bucket from -Inf to Inf, calculate the mean as min value.
		[InlineData(new double[0], 1, new[] { 1.2 }, 1.2, 1.2, 1.2)]
		// min: Only the last bucket has a value, use the lower bound.
		[InlineData(new[] { 1d, 2d, 3d, 4d, 5d }, 3, new[] { 5.1, 5.1, 5.4 }, 15.6, 5, 5.2)]
		// max: Values between the last two boundaries.
		[InlineData(new[] { 1d, 2d, 3d, 4d, 5d }, 6, new[] { 1.5, 3.2, 3.2, 3.5, 4.8, 4.9 }, 21.1, 1d, 5d)]
		// max: Values between the last two boundaries.
		[InlineData(new[] { 1d, 2d, 3d, 4d, 5d }, 8, new[] { 0.5, 3.1, 3.1, 3.1, 6, 6, 6, 6.7 }, 34.5, 1, 5)]
		// max: Only the last bucket has values, use the mean (10.1) Otherwise, the max would be estimated as 5, and max >= avg would be violated.
		[InlineData(new[] { 1d, 2d, 3d, 4d, 5d }, 2, new[] { 5.2, 15 }, 20.2, 5, 10.1)]
		// max: Just one bucket from -Inf to Inf, calculate the mean as max value.
		[InlineData(new double[0], 4, new[] { 2, 2, 2, 2.8 }, 8.8, 2.2, 2.2)]
		// max: Just one bucket from -Inf to Inf, calculate the mean as max value.
		[InlineData(new double[0], 1, new[] { 1.2 }, 1.2, 1.2, 1.2)]
		// max: Max is larger than the sum, use the estimated boundary.
		[InlineData(new[] { 0d, 5d }, 2, new[] { 1, 1.3 }, 2.3, 0, 5d)]
		// max: Only the first bucket has a value, use the upper bound.
		[InlineData(new[] {1d, 2d, 3d, 4d, 5d}, 3, new [] {0.2, 0.4, 0.9}, 1.5, 0.5, 1)]
		public async Task Export_Histogram_ShouldSetMinAndMaxCorrectly(double[] boundaries, int count, double[] values, double sum, double min, double max)
		{
			// Arrange
			using var meter = new Meter(Guid.NewGuid().ToString(), "0.0.1");
			var exportedMetrics = new List<Metric>();

			using var meterProvider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddInMemoryExporter(exportedMetrics,
					options => options.TemporalityPreference = MetricReaderTemporalityPreference.Delta)
				.AddView(
					instrumentName: "histogram",
					new ExplicitBucketHistogramConfiguration { Boundaries = boundaries })
				.Build();

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(r => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var histogram = meter.CreateHistogram<double>("histogram");

			foreach (var value in values)
			{
				histogram.Record(value, _attributes);
			}

			// Act
			meterProvider.ForceFlush();
			sut.Export(new Batch<Metric>(exportedMetrics.ToArray(), exportedMetrics.Count));

			// Assert
			var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

			var expected = $"histogram,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry gauge,min={min},max={max},sum={sum},count={count} {point.TimeStamp}";
			var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
			Assert.Equal(expected, actualMetricString);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(1),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());

			AssertExportRequest(actualRequestMessage);
		}

		private static Mock<HttpMessageHandler> SetupHttpMock(
			Action<HttpRequestMessage>? setter = null,
			HttpStatusCode? statusCode = null,
			HttpContent? content = null)
		{
			var mockMessageHandler = new Mock<HttpMessageHandler>();

			mockMessageHandler.Protected()
				.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
					ItExpr.IsAny<CancellationToken>())
				.ReturnsAsync(new HttpResponseMessage { StatusCode = statusCode ?? HttpStatusCode.OK, Content = content ?? new StringContent("test") })
				.Callback((HttpRequestMessage r, CancellationToken _) =>
				{
					setter?.Invoke(r);
				});


			return mockMessageHandler;
		}

		private static void AssertExportRequest(HttpRequestMessage actual, string? endpoint = null,
			string? apiToken = null)
		{
			Assert.Equal(HttpMethod.Post, actual.Method);
			Assert.Single(actual.Headers.GetValues("User-Agent"));
			Assert.Equal("opentelemetry-metric-dotnet", actual.Headers.GetValues("User-Agent").First());

			Assert.Equal(endpoint ?? DynatraceMetricApiConstants.DefaultOneAgentEndpoint,
				actual.RequestUri!.AbsoluteUri);

			if (apiToken == null)
			{
				Assert.False(actual.Headers.Contains("Api-Token"));
			}
			else
			{
				Assert.Equal($"Api-Token {apiToken}", actual.Headers.GetValues("Authorization").First());
			}
		}
	}
}
