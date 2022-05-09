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
		private readonly MeterProvider _provider;
		private readonly List<Metric> _exportedMetrics;
		private readonly Meter _meter;

		public DynatraceMetricsExporterTests()
		{
			_meter = new Meter(Guid.NewGuid().ToString(), "0.0.1");
			_exportedMetrics = new List<Metric>();

			_provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(_meter.Name)
				.AddInMemoryExporter(_exportedMetrics, options => options.TemporalityPreference = MetricReaderTemporalityPreference.Delta)
				.Build();
		}

		public void Dispose()
		{
			_meter.Dispose();
			_provider.Dispose();
		}

		[Fact]
		public async Task Export_WithDefaultOptions_ShouldSendRequestToOneAgent()
		{
			// Arrange
			var counter = _meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);
			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			// Act
			_provider.ForceFlush();
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
				(HttpRequestMessage r) => actualRequestMessage = r,
				HttpStatusCode.InternalServerError);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act
			_provider.ForceFlush();
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
			_provider.ForceFlush();
			var exportResult = sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
			Assert.Equal(ExportResult.Failure, exportResult);
		}

		[Fact]
		public async Task Export_WithUriAndTokenOptions_ShouldSendRequestToUrlWithToken()
		{
			// Arrange
			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(
				new DynatraceExporterOptions { Url = "http://my.url", ApiToken = "test-token" }, null,
				new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act
			_provider.ForceFlush();
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
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(
				new DynatraceExporterOptions { Prefix = "my.prefix" }, null,
				new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act
			_provider.ForceFlush();
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
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var defaultDimensions = new KeyValuePair<string, string>[] { new("d1", "v1"), new("d2", "v2"), };

			var sut = new DynatraceMetricsExporter(
				new DynatraceExporterOptions { DefaultDimensions = defaultDimensions }, null,
				new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act
			_provider.ForceFlush();
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
			_provider.ForceFlush();
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
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<int>("counter");

			for (var i = 10; i <= 30; i += 10)
			{
				counter.Add(i, _attributes);

				_exportedMetrics.Clear();
				_provider.ForceFlush();
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
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var counterA = _meter.CreateCounter<int>("counterA");
			counterA.Add(10, _attributes);

			var counterB = _meter.CreateCounter<int>("counterB");
			counterB.Add(20, _attributes);

			// Act
			_provider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));

			// Assert
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
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<long>("counter");
			counter.Add(10, _attributes);

			// Act
			_provider.ForceFlush();
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
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var counter = _meter.CreateCounter<double>("double_counter");
			counter.Add(10.3, _attributes);

			// Act
			_provider.ForceFlush();
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
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			// When exported twice, this obs counter will output
			// 10 -> 20. Since we expect deltas to be calculated by the SDK
			// we assert both exports to be 10
			var i = 1;
			_meter.CreateObservableCounter("obs_counter",
				() => new List<Measurement<long>> { new(i++ * 10, _attributes) });

			// Perform two exports to ensure deltas are exported correctly
			_provider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));
			await AssertLines(10);
			_exportedMetrics.Clear();

			_provider.ForceFlush();
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
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			// When exported twice, this obs counter will output
			// 10.3 -> 20.6. Since we expect deltas to be calculated by the SDK
			// we assert both exports to be 10.3
			var i = 1;
			_meter.CreateObservableCounter("double_obs_counter",
				() => new List<Measurement<double>> { new(i++ * 10.3, _attributes) });

			// Perform two exports to ensure deltas are exported correctly
			_provider.ForceFlush();
			sut.Export(new Batch<Metric>(_exportedMetrics.ToArray(), _exportedMetrics.Count));
			await AssertLines(10.3);
			_exportedMetrics.Clear();

			_provider.ForceFlush();
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
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			_meter.CreateObservableGauge("gauge",
				() => new List<Measurement<long>> { new(10, _attributes) });

			// Act
			_provider.ForceFlush();
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
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			_meter.CreateObservableGauge("double_gauge",
				() => new List<Measurement<double>> { new(10.3, _attributes), });

			// Act
			_provider.ForceFlush();
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
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			// default bounds are 0, 5, 10, 25, 50, 75, 100, 250, 500
			var histogram = _meter.CreateHistogram<long>("histogram");

			histogram.Record(1, _attributes);
			histogram.Record(6, _attributes);
			histogram.Record(11, _attributes);
			histogram.Record(21, _attributes);

			// Act
			_provider.ForceFlush();
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

			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddInMemoryExporter(exportedMetrics,
					options => options.TemporalityPreference = MetricReaderTemporalityPreference.Delta)
				.AddView(
					instrumentName: "histogram",
					new ExplicitBucketHistogramConfiguration { Boundaries = new double[] { 0.1, 1.2, 3.4, 5.6 } })
				.Build();

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var histogram = meter.CreateHistogram<double>("histogram");

			histogram.Record(0.2, _attributes);
			histogram.Record(1.4, _attributes);
			histogram.Record(2, _attributes);
			histogram.Record(4, _attributes);

			// Act
			provider.ForceFlush();
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
		// min: A value between the first two boundaries (1.5)
		[InlineData(new[] { 1.5, 3.5, 3.5, 6 }, 1d, 5d, 14.5, 4, new[] { 1d, 2d, 3d, 4d, 5d })]
		// min: lowest bucket has value, use the first boundary as estimation instead of -Inf
		// max: last bucket has value, use the last boundary as estimation instead of Inf
		[InlineData(new[] { 0.5, 3.5, 3.5, 6 }, 1d, 5d, 13.5, 4, new[] { 1d, 2d, 3d, 4d, 5d })]
		// min: lowest bucket (-Inf, 1) has values, mean is lower than the lowest bucket bound and smaller than the sum
		[InlineData(new[] { 0.9, 0.9 }, 0.9, 1, 1.8, 2, new[] { 1d, 2d, 3d, 4d, 5d })]
		// min: lowest bucket (-Inf, 0) has values, sum is lower than the lowest bucket bound
		[InlineData(new[] { 0.1, 0.1 }, 0.1, 0.2, 0.2, 2, new[] { 1d, 2d, 3d, 4d, 5d })]
		// min: just one bucket from -Inf to Inf, calc the mean as min value.
		// max: just one bucket from -Inf to Inf, calc the mean as max value.
		[InlineData(new[] { 1d, 2d, 3d }, 2, 2, 6, 3, new double[] { })]
		// min: just one bucket from -Inf to Inf, with a count of 1, use sum.
		// max: just one bucket from -Inf to Inf, with a count of 1, use sum.
		[InlineData(new[] { 6d }, 6, 6, 6, 1, new double[] { })]
		// min: only the last bucket has a value (5, +Inf), use the last boundary as an estimation instead of Inf
		// max: only the last bucket has a value (5, +Inf), use the last boundary as an estimation instead of Inf
		[InlineData(new[] { 6d }, 5, 5, 6, 1, new[] { 1d, 2d, 3d, 4d, 5d })]
		// max: A value between the last two boundaries.
		[InlineData(new[] { 1.5, 3.5, 3.5, 4.5 }, 1d, 5d, 13, 4, new[] { 1d, 2d, 3d, 4d, 5d })]
		// max: just values in one bucket, but lower than boundary, use sum.
		[InlineData(new[] { 1d, 1d }, 0d, 2d, 2, 2, new[] { -5d, 0, 5d })]
		public async Task Export_Histogram_ShouldSetMinAndMaxCorrectly(double[] values, double min, double max, double sum, int count, double[] boundaries)
		{
			// Arrange
			using var meter = new Meter(Guid.NewGuid().ToString(), "0.0.1");
			var exportedMetrics = new List<Metric>();

			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddInMemoryExporter(exportedMetrics,
					options => options.TemporalityPreference = MetricReaderTemporalityPreference.Delta)
				.AddView(
					instrumentName: "histogram",
					new ExplicitBucketHistogramConfiguration { Boundaries = boundaries })
				.Build();

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var histogram = meter.CreateHistogram<double>("histogram");

			foreach (var value in values)
			{
				histogram.Record(value, _attributes);
			}

			// Act
			provider.ForceFlush();
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
