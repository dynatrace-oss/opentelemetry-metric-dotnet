
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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using OpenTelemetry.Metrics.Export;
using Xunit;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests
{
	public class DynatraceMetricsExporterTests
	{
		[Fact]
		public async void TestDefaultOptions()
		{
			var mockMessageHandler = new Mock<HttpMessageHandler>();
			// this var will hold the actual passed in params
			HttpRequestMessage actualRequestMessage = null;

			mockMessageHandler.Protected()
			.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				StatusCode = HttpStatusCode.OK,
				Content = new StringContent("test")
			})
			.Callback((HttpRequestMessage r, CancellationToken _) => actualRequestMessage = r);

			var exporter = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			await exporter.ExportAsync(new List<Metric> { CreateMetric() }, CancellationToken.None);
			mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());

			Assert.Equal(HttpMethod.Post, actualRequestMessage.Method);
			Assert.Equal(DynatraceMetricApiConstants.DefaultOneAgentEndpoint, actualRequestMessage.RequestUri.AbsoluteUri);
			Assert.False(actualRequestMessage.Headers.Contains("Api-Token"));
			Assert.True(actualRequestMessage.Headers.Contains("User-Agent"));
			Assert.Single(actualRequestMessage.Headers.GetValues("User-Agent"));
			Assert.Equal("opentelemetry-metric-dotnet", actualRequestMessage.Headers.GetValues("User-Agent").First());
			Assert.Equal("namespace1.metric1,label1=value1,label2=value2,dt.metrics.source=opentelemetry count,delta=100 1604660628881" + Environment.NewLine, actualRequestMessage.Content.ReadAsStringAsync().Result);
		}

		[Fact]
		public async void TestUriAndToken()
		{
			var mockMessageHandler = new Mock<HttpMessageHandler>();
			HttpRequestMessage req = null;

			mockMessageHandler.Protected()
			.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				StatusCode = HttpStatusCode.OK,
				Content = new StringContent("test")
			})
			.Callback((HttpRequestMessage r, CancellationToken _) => req = r);

			var exporter = new DynatraceMetricsExporter(new DynatraceExporterOptions { Url = "http://my.url", ApiToken = "test-token" }, null, new HttpClient(mockMessageHandler.Object));

			await exporter.ExportAsync(new List<Metric> { CreateMetric() }, CancellationToken.None);
			mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
			Assert.Equal("http://my.url/", req.RequestUri.AbsoluteUri);
			Assert.True(req.Headers.Contains("Authorization"));
			Assert.Single(req.Headers.GetValues("Authorization"));
			Assert.Equal("Api-Token test-token", req.Headers.GetValues("Authorization").First());
			Assert.Equal("namespace1.metric1,label1=value1,label2=value2,dt.metrics.source=opentelemetry count,delta=100 1604660628881" + Environment.NewLine, req.Content.ReadAsStringAsync().Result);
		}

		[Fact]
		public async void TestSendInBatches()
		{
			var mockMessageHandler = new Mock<HttpMessageHandler>();
			mockMessageHandler.Protected()
			.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				StatusCode = HttpStatusCode.OK,
				Content = new StringContent("test")
			});

			var exporter = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));
			var metrics = new List<Metric>();
			for (int i = 0; i < 1001; i++)
			{
				metrics.Add(CreateMetric());
			}

			await exporter.ExportAsync(metrics, CancellationToken.None);

			// for more than 1000 lines, SendAsync is called twice.
			mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(2), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
		}

		[Fact]
		public async void TestExportMultipleWithPrefix()
		{
			var mockMessageHandler = new Mock<HttpMessageHandler>();
			HttpRequestMessage req = null;

			mockMessageHandler.Protected()
			.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				StatusCode = HttpStatusCode.OK,
				Content = new StringContent("test")
			})
			.Callback((HttpRequestMessage r, CancellationToken _) => req = r);

			var exporter = new DynatraceMetricsExporter(new DynatraceExporterOptions { Prefix = "my.prefix" }, null, new HttpClient(mockMessageHandler.Object));

			await exporter.ExportAsync(CreateMetrics(), CancellationToken.None);
			var expectedString = "my.prefix.namespace1.metric1,dt.metrics.source=opentelemetry count,delta=100 1604660628881" + Environment.NewLine + "my.prefix.namespace2.metric2,dt.metrics.source=opentelemetry count,delta=200 1604660628881" + Environment.NewLine;

			mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post), ItExpr.IsAny<CancellationToken>());
			Assert.Equal(DynatraceMetricApiConstants.DefaultOneAgentEndpoint, req.RequestUri.AbsoluteUri);
			Assert.False(req.Headers.Contains("Api-Token"));
			Assert.Equal(expectedString, req.Content.ReadAsStringAsync().Result);
		}

		[Fact]
		public async void TestExportMultiDataMetric()
		{
			var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime;

			var metric = new Metric("namespace1", "metric1", "Description", AggregationType.LongSum);
			metric.Data.Add(new Int64SumData
			{
				Sum = 100,
				Timestamp = timestamp
			});
			metric.Data.Add(new Int64SumData
			{
				Sum = 101,
				Timestamp = timestamp
			});

			var mockMessageHandler = new Mock<HttpMessageHandler>();
			HttpRequestMessage req = null;

			mockMessageHandler.Protected()
			.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				StatusCode = HttpStatusCode.OK,
				Content = new StringContent("test")
			})
			.Callback((HttpRequestMessage r, CancellationToken _) => req = r);

			var exporter = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

			await exporter.ExportAsync(new List<Metric> { metric }, CancellationToken.None);
			var expectedString = "namespace1.metric1,dt.metrics.source=opentelemetry count,delta=100 1604660628881" + Environment.NewLine +
								 "namespace1.metric1,dt.metrics.source=opentelemetry count,delta=101 1604660628881" + Environment.NewLine;

			mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post), ItExpr.IsAny<CancellationToken>());
			Assert.Equal(expectedString, req.Content.ReadAsStringAsync().Result);
		}

		[Fact]
		public async void TestExportNullNameMetric()
		{
			var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime;

			var metric = new Metric(null, null, null, AggregationType.LongSum);
			metric.Data.Add(new Int64SumData
			{
				Sum = 100,
				Timestamp = timestamp
			});

			var mockMessageHandler = new Mock<HttpMessageHandler>();
			HttpRequestMessage req = null;

			mockMessageHandler.Protected()
			.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				StatusCode = HttpStatusCode.OK,
				Content = new StringContent("test")
			})
			.Callback((HttpRequestMessage r, CancellationToken _) => req = r);

			var mockLogger = new Mock<ILogger<DynatraceMetricsExporter>>();

			var exporter = new DynatraceMetricsExporter(null, mockLogger.Object, new HttpClient(mockMessageHandler.Object));

			await exporter.ExportAsync(new List<Metric> { metric }, CancellationToken.None);

			mockMessageHandler.Protected().Verify("SendAsync", Times.Never(), ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post), ItExpr.IsAny<CancellationToken>());
			mockLogger.Verify(x => x.Log(It.Is<LogLevel>(level => level == LogLevel.Warning),
										 It.IsAny<EventId>(),
										 It.Is<It.IsAnyType>((value, type) => value.ToString().Contains("Mapping")),
										 It.IsAny<Exception>(),
										 It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Exactly(1));
		}

		[Fact]
		public async void TestExportLargeMetric()
		{
			var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime;

			var dimensions = new List<KeyValuePair<string, string>>();
			// 20 dimensions of ~ 100 characters should result in lines with more than 2000 characters
			for (var i = 0; i < 20; i++)
			{
				// creates a dimension that takes up a little more than 100 characters
				dimensions.Add(new KeyValuePair<string, string>(new string('a', 50) + i, new string('b', 50) + i));
			}

			var metric = CreateMetric();
			metric.Data[0].Labels = dimensions;

			var mockMessageHandler = new Mock<HttpMessageHandler>();
			HttpRequestMessage req = null;

			mockMessageHandler.Protected()
			.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				StatusCode = HttpStatusCode.OK,
				Content = new StringContent("test")
			})
			.Callback((HttpRequestMessage r, CancellationToken _) => req = r);

			var mockLogger = new Mock<ILogger<DynatraceMetricsExporter>>();

			var exporter = new DynatraceMetricsExporter(null, mockLogger.Object, new HttpClient(mockMessageHandler.Object));

			await exporter.ExportAsync(new List<Metric> { metric }, CancellationToken.None);

			mockMessageHandler.Protected().Verify("SendAsync", Times.Never(), ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post), ItExpr.IsAny<CancellationToken>());
			mockLogger.Verify(x => x.Log(It.Is<LogLevel>(level => level == LogLevel.Warning),
										 It.IsAny<EventId>(),
										 It.Is<It.IsAnyType>((value, type) => value.ToString().Contains("Serialization")),
										 It.IsAny<Exception>(),
										 It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Exactly(1));
		}

		private List<Metric> CreateMetrics()
		{
			var metrics = new List<Metric>();

			var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime;

			var metric1 = new Metric("namespace1", "metric1", "Description", AggregationType.LongSum);
			metric1.Data.Add(new Int64SumData
			{
				Sum = 100,
				Timestamp = timestamp
			});

			metrics.Add(metric1);

			var metric2 = new Metric("namespace2", "metric2", "Description", AggregationType.LongSum);
			metric2.Data.Add(new Int64SumData
			{
				Sum = 200,
				Timestamp = timestamp
			});
			metrics.Add(metric2);

			return metrics;
		}

		private Metric CreateMetric()
		{
			var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime;

			var labels = new List<KeyValuePair<string, string>> {
				new KeyValuePair<string, string>("label1", "value1"),
				new KeyValuePair<string, string>("label2", "value2")
			};
			var metric = new Metric("namespace1", "metric1", "Description", AggregationType.LongSum);
			metric.Data.Add(new Int64SumData
			{
				Labels = labels,
				Sum = 100,
				Timestamp = timestamp
			});

			return metric;
		}
	}
}
