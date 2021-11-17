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
using System.Net;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics
{
	/// <summary>
	/// Offers extensions around <see cref="ILogger"/> for high-performance logging.
	/// <see href="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/loggermessage?view=aspnetcore-6.0"/>
	/// </summary>
	internal static class LoggerExtensions
	{
		private static readonly Action<ILogger, string, Exception> _dynatraceMetricUrl =
			LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, nameof(DynatraceMetricUrl)),
				"Dynatrace Metrics Url: {Url}");

		private static readonly Action<ILogger, MetricType, Exception> _unknownMetricType =
			LoggerMessage.Define<MetricType>(LogLevel.Warning, new EventId(2, nameof(InvalidMetricType)),
				"Tried to serialize metric of type {MetricType}. The Dynatrace metrics exporter does not handle metrics of that type at this time.");

		private static readonly Action<ILogger, HttpStatusCode, string, Exception> _sendMetricErrorResponse =
			LoggerMessage.Define<HttpStatusCode, string>(LogLevel.Error, new EventId(3, nameof(ReceivedErrorResponse)),
				"Received an error response while sending metrics. StatusCode: {StatusCode} Response: {Response}");

		private static readonly Action<ILogger, Exception> _sendMetricException =
			LoggerMessage.Define(LogLevel.Error, new EventId(4, nameof(FailedSendingMetricLines)),
				"Error sending metrics to Dynatrace.");

		private static readonly Action<ILogger, string, Exception> _serializeMetricException =
			LoggerMessage.Define<string>(LogLevel.Warning, new EventId(5, nameof(FailedToSerializeMetric)),
				"Skipping metric with the original name '{MetricName}'");

		private static readonly Action<ILogger, string, Exception> _unsupportedMetricType =
			LoggerMessage.Define<string>(LogLevel.Warning, new EventId(6, nameof(UnsupportedMetricType)),
				"Skipping unsupported dimension with value type '{MetricType}'");

		private static readonly Action<ILogger, string, Exception> _receivedCumulativeValue =
			LoggerMessage.Define<string>(LogLevel.Warning, new EventId(7, nameof(ReceivedCumulativeValue)),
				"Received metric: '{MetricName}' with cumulative aggregation temporality. Exporting as gauge");

		internal static void DynatraceMetricUrl(this ILogger logger, string url)
			=> _dynatraceMetricUrl(logger, url, null);

		internal static void InvalidMetricType(this ILogger logger, MetricType metricType)
			=> _unknownMetricType(logger, metricType, null);

		internal static void ReceivedErrorResponse(this ILogger logger, HttpStatusCode statusCode, string response)
			=> _sendMetricErrorResponse(logger, statusCode, response, null);

		internal static void FailedSendingMetricLines(this ILogger logger, Exception exception)
			=> _sendMetricException(logger, exception);

		internal static void FailedToSerializeMetric(this ILogger logger, string metricName, Exception exception)
			=> _serializeMetricException(logger, metricName, exception);

		internal static void UnsupportedMetricType(this ILogger logger, string metricType)
			=> _unsupportedMetricType(logger, metricType, null);

		internal static void ReceivedCumulativeValue(this ILogger logger, string metricName)
			=> _receivedCumulativeValue(logger, metricName, null);
	}
}
