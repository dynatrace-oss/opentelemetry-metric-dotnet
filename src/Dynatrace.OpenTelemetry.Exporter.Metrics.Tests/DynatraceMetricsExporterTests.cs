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
	public class DynatraceMetricsExporterTests
	{
		private readonly TagList _attributes = new()
		{
			{ "attr1", "v1" },
			{ "attr2", "v2" },
		};

		[Fact]
		public async Task Export_WithDefaultOptions_ShouldSendRequestToOneAgent()
		{
			// Arrange
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var counter = meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			var exportedMetrics = metricReader.GetExportedMetrics();
			var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock(
				(HttpRequestMessage r) => actualRequestMessage = r,
				HttpStatusCode.InternalServerError);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var counter = meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			Assert.Equal(ExportResult.Failure, metricReader.ExportResult);

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			var mockMessageHandler = new Mock<HttpMessageHandler>();
			mockMessageHandler.Protected()
				.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
				.Throws(new HttpRequestException());

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var counter = meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			Assert.Equal(ExportResult.Failure, metricReader.ExportResult);
		}

		[Fact]
		public async Task Export_WithUriAndTokenOptions_ShouldSendRequestToUrlWithToken()
		{
			// Arrange
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(
				new DynatraceExporterOptions { Url = "http://my.url", ApiToken = "test-token" }, null,
				new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var counter = meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			var exportedMetrics = metricReader.GetExportedMetrics();
			var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(
				new DynatraceExporterOptions { Prefix = "my.prefix" }, null,
				new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var counter = meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			var exportedMetrics = metricReader.GetExportedMetrics();
			var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var defaultDimensions = new KeyValuePair<string, string>[]
			{
				new ("d1", "v1"),
				new ("d2", "v2"),
			};

			var sut = new DynatraceMetricsExporter(
				new DynatraceExporterOptions { DefaultDimensions = defaultDimensions }, null,
				new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var counter = meter.CreateCounter<int>("counter");
			counter.Add(10, _attributes);

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			var exportedMetrics = metricReader.GetExportedMetrics();
			var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

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

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var counter = meter.CreateCounter<int>("counter");
			counter.Add(10, dimensions.ToArray());

			// Act - Reader will call our exporter
			metricReader.Collect();

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
		public void Export_OverrideAggregationTemporality_ShouldThrowArgumentException()
		{
			// Arrange
			var sut = new DynatraceMetricsExporter();

			// It should not be possible to override the reader with a different temporality
			// https://github.com/open-telemetry/opentelemetry-specification/blob/v1.8.0/specification/metrics/sdk.md#temporality-override-rules
			var ex = Assert.Throws<ArgumentException>(() => new PeriodicExportingMetricReader(sut)
			{
				PreferredAggregationTemporality = AggregationTemporality.Cumulative
			});

			Assert.Contains(
				"PreferredAggregationTemporality Cumulative and SupportedAggregationTemporality Delta are incompatible",
				ex.Message);

			// It should not be possible to override the reader with a different temporality
			// https://github.com/open-telemetry/opentelemetry-specification/blob/v1.8.0/specification/metrics/sdk.md#temporality-override-rules
			ex = Assert.Throws<ArgumentException>(() => new PeriodicExportingMetricReader(sut)
			{
				SupportedAggregationTemporality = AggregationTemporality.Cumulative
			});

			Assert.Contains(
				"PreferredAggregationTemporality Delta and SupportedAggregationTemporality Cumulative are incompatible",
				ex.Message);
		}

		[Fact]
		public async Task Export_SecondCumulativeMetricReader_DynatraceExporterUsesDelta()
		{
			// Arrange
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				// configure another reader with Cumulative aggregation
				.AddConsoleExporter(opt => opt.AggregationTemporality = AggregationTemporality.Cumulative)
				.Build();

			var counter = meter.CreateCounter<int>("counter");

			for (var i = 10; i <= 30; i += 10)
			{
				counter.Add(i, _attributes);

				metricReader.Collect();

				// Assert
				var exportedMetrics = metricReader.GetExportedMetrics();
				var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
		public async Task Export_SimulateMultipleExports_ShouldExportCorrectDelta()
		{
			// Arrange
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var counter = meter.CreateCounter<int>("counter");

			for (var i = 10; i <= 30; i += 10)
			{
				counter.Add(i, _attributes);

				metricReader.Collect();

				// Assert
				var exportedMetrics = metricReader.GetExportedMetrics();
				var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var counterA = meter.CreateCounter<int>("counterA");
			counterA.Add(10, _attributes);

			var counterB = meter.CreateCounter<int>("counterB");
			counterB.Add(20, _attributes);

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			var exportedMetrics = metricReader.GetExportedMetrics();
			var pointA = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();
			var pointB = MetricTest.FromMetricPoints(exportedMetrics.Last().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			// Delta is already the preferred/supported temporality for our exporter
			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var counter = meter.CreateCounter<long>("counter");
			counter.Add(10, _attributes);

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			var exportedMetrics = metricReader.GetExportedMetrics();
			var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			// Delta is already the preferred/supported temporality for our exporter
			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var counter = meter.CreateCounter<double>("double_counter");
			counter.Add(10.3, _attributes);

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			var exportedMetrics = metricReader.GetExportedMetrics();
			var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			// Delta is already the preferred/supported temporality for our exporter
			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var i = 1;
			var observableCounter = meter.CreateObservableCounter("obs_counter", () =>
			{
				return new List<Measurement<long>>()
				{
					new(i++ * 10, _attributes)
				};
			});

			// Perform two exports to ensure deltas are exported correctly
			metricReader.Collect();
			await AssertLines(metricReader, 10);

			metricReader.Collect();
			await AssertLines(metricReader, 10);

			async Task AssertLines(TestMetricReader metricReader, long expectedValue)
			{
				var exportedMetrics = metricReader.GetExportedMetrics();
				var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			// Delta is already the preferred/temporality temporality for our exporter
			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var i = 1;
			var observableCounter = meter.CreateObservableCounter("double_obs_counter", () =>
			{
				return new List<Measurement<double>>()
				{
					new(i++ * 10.3, _attributes)
				};
			});

			// Perform two exports to ensure deltas are exported correctly
			metricReader.Collect();
			await AssertLines(metricReader, 10.3);

			metricReader.Collect();
			await AssertLines(metricReader, 10.3);

			async Task AssertLines(TestMetricReader metricReader, double expectedValue)
			{
				var exportedMetrics = metricReader.GetExportedMetrics();
				var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var observableCounter = meter.CreateObservableGauge("gauge", () =>
			{
				return new List<Measurement<long>>()
				{
					new (10, _attributes)
				};
			});

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			var exportedMetrics = metricReader.GetExportedMetrics();
			var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			var observableCounter = meter.CreateObservableGauge("double_gauge", () =>
			{
				return new List<Measurement<double>>()
				{
					new (10.3, _attributes),
				};
			});

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			var exportedMetrics = metricReader.GetExportedMetrics();
			var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.Build();

			// default bounds are 0, 5, 10, 25, 50, 75, 100, 250, 500
			var histogram = meter.CreateHistogram<long>("histogram");

			histogram.Record(1, _attributes);
			histogram.Record(6, _attributes);
			histogram.Record(11, _attributes);
			histogram.Record(21, _attributes);

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			var exportedMetrics = metricReader.GetExportedMetrics();
			var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

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
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.AddView(
				instrumentName: "histogram",
				new ExplicitBucketHistogramConfiguration { Boundaries = new double[] { 0.1, 1.2, 3.4, 5.6 } })
				.Build();

			var histogram = meter.CreateHistogram<double>("histogram");

			histogram.Record(0.2, _attributes);
			histogram.Record(1.4, _attributes);
			histogram.Record(2, _attributes);
			histogram.Record(4, _attributes);

			// Act - Reader will call our exporter
			metricReader.Collect();

			// Assert
			var exportedMetrics = metricReader.GetExportedMetrics();
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

		[Fact]
		public async Task Export_View_CounterWithDeltaTemporality()
		{
			// Arrange
			using var meter = new Meter(TestUtils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);

			var sut = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			var metricReader = new TestMetricReader(sut);
			using var provider = Sdk.CreateMeterProviderBuilder()
				.AddMeter(meter.Name)
				.AddReader(metricReader)
				.AddView(instrumentName: "counter", name: "myview")
				.Build();

			var counter = meter.CreateCounter<long>("counter");

			counter.Add(10, _attributes);
			metricReader.Collect();
			await AssertLines(metricReader, 10);

			counter.Add(20, _attributes);
			metricReader.Collect();
			await AssertLines(metricReader, 20);

			counter.Add(30, _attributes);
			metricReader.Collect();
			await AssertLines(metricReader, 30);

			async Task AssertLines(TestMetricReader metricReader, long expectedValue)
			{
				var exportedMetrics = metricReader.GetExportedMetrics();
				var point = MetricTest.FromMetricPoints(exportedMetrics.First().GetMetricPoints()).First();

				var expected = $"myview,attr1=v1,attr2=v2,dt.metrics.source=opentelemetry count,delta={expectedValue} {point.TimeStamp}";
				var actualMetricString = await actualRequestMessage.Content!.ReadAsStringAsync();
				Assert.Equal(expected, actualMetricString);
			}

			AssertExportRequest(actualRequestMessage);

			mockMessageHandler.Protected().Verify(
				"SendAsync",
				Times.Exactly(3),
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>());
		}

		private static Mock<HttpMessageHandler> SetupHttpMock(
			Action<HttpRequestMessage>? setter = null,
			HttpStatusCode? statusCode = null,
			HttpContent? content = null)
		{
			var mockMessageHandler = new Mock<HttpMessageHandler>();

			mockMessageHandler.Protected()
				.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
				.ReturnsAsync(new HttpResponseMessage
				{
					StatusCode = statusCode ?? HttpStatusCode.OK,
					Content = content ?? new StringContent("test")
				})
				.Callback((HttpRequestMessage r, CancellationToken _) =>
				{
					setter?.Invoke(r);
				});



			return mockMessageHandler;
		}

		private static void AssertExportRequest(HttpRequestMessage actual, string? endpoint = null, string? apiToken = null)
		{
			Assert.Equal(HttpMethod.Post, actual.Method);
			Assert.Single(actual.Headers.GetValues("User-Agent"));
			Assert.Equal("opentelemetry-metric-dotnet", actual.Headers.GetValues("User-Agent").First());

			Assert.Equal(endpoint ?? DynatraceMetricApiConstants.DefaultOneAgentEndpoint, actual.RequestUri!.AbsoluteUri);

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
