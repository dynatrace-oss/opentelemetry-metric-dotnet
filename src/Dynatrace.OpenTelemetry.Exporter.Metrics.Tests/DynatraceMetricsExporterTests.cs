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
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
		private readonly KeyValuePair<string, object?>[] _attributes = new KeyValuePair<string, object?>[]
		{
			new ("attr1", "v1"), new ("attr2", "v2"),
		};

		[Fact]
		public async Task ExportAsync_WithDefaultOptions_ShouldSendRequestToOneAgent()
		{
			// Arrange
			using var meter = new Meter(Utils.GetCurrentMethodName(), "0.0.1");

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
		public void ExportAsync_FailedExport_ReturnsFailedExportResult()
		{
			// Arrange
			using var meter = new Meter(Utils.GetCurrentMethodName(), "0.0.1");

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
		public async Task ExportAsync_WithUriAndTokenOptions_ShouldSendReqeustToUrlWithToken()
		{
			// Arrange
			using var meter = new Meter(Utils.GetCurrentMethodName(), "0.0.1");

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
		public async Task ExportAsync_WithPrefixOptions_ShouldAppendPrefixToMetrics()
		{
			// Arrange
			using var meter = new Meter(Utils.GetCurrentMethodName(), "0.0.1");

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
		public async Task ExportAsync_WithDefaultDimensions_ShouldAddDimensionsToMetrics()
		{
			// Arrange
			using var meter = new Meter(Utils.GetCurrentMethodName(), "0.0.1");

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
		public void ExportAsync_WithTooLargeMetric_ShouldNotSendRequest()
		{
			// Arrange
			using var meter = new Meter(Utils.GetCurrentMethodName(), "0.0.1");

			HttpRequestMessage actualRequestMessage = null!;
			var mockMessageHandler = SetupHttpMock((HttpRequestMessage r) => actualRequestMessage = r);
			var mockLogger = new Mock<ILogger<DynatraceMetricsExporter>>();
			mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true);

			// 20 dimensions of ~ 100 characters should result in lines with more than 2000 characters
			var dimensions = new List<KeyValuePair<string, object?>>();
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
		public async Task ExportAsync_WithMultiDataMetric_ShouldSendRequestWithMultipleLines()
		{
			// Arrange
			using var meter = new Meter(Utils.GetCurrentMethodName(), "0.0.1");

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

		private static Mock<HttpMessageHandler> SetupHttpMock(
			Action<HttpRequestMessage> setter,
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
					setter(r);
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
