using System;
using System.Net;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics
{
	internal static class LoggerExtensions
	{
		private static Action<ILogger, string, Exception> _dynatraceMetricUrl;
		private static Action<ILogger, MetricType, Exception> _unknownMetricType;
		private static Action<ILogger, HttpStatusCode, string, Exception> _sendMetricErrorResponse;
		private static Action<ILogger, Exception> _sendMetricException;
		private static Action<ILogger, string, Exception> _serializeMetricException;
		private static Action<ILogger, string, Exception> _unsupportedMetricType;

		static LoggerExtensions()
		{
			_dynatraceMetricUrl = LoggerMessage.Define<string>(
				LogLevel.Debug,
				new EventId(1, nameof(InvalidMetricType)),
				"Dynatrace Metrics Url: {Url}"
				);

			_unknownMetricType = LoggerMessage.Define<MetricType>(
				LogLevel.Warning,
				new EventId(2, nameof(InvalidMetricType)),
				"Tried to serialize metric of type {MetricType}. The Dynatrace metrics exporter does not handle metrics of that type at this time."
				);

			_sendMetricErrorResponse = LoggerMessage.Define<HttpStatusCode, string>(
				LogLevel.Error,
				new EventId(3, nameof(InvalidMetricType)),
				"Received an error response while sending metrics. StatusCode: {StatusCode} Response: {Response}"
				);

			_sendMetricException = LoggerMessage.Define(
				LogLevel.Error,
				new EventId(4, nameof(InvalidMetricType)),
				"Error sending metrics to Dynatrace."
				);

			_serializeMetricException = LoggerMessage.Define<string>(
				LogLevel.Warning,
				new EventId(5, nameof(FailedToSerializeMetric)),
				"Skipping metric with the original name '{MetricName}'"
				);

			_unsupportedMetricType = LoggerMessage.Define<string>(
				LogLevel.Warning,
				new EventId(6, nameof(FailedToSerializeMetric)),
				"Skipping unsupported dimension with value type '{MetricType}'"
				);
		}

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
	}
}
