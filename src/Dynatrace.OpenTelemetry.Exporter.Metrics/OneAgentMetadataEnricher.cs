using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Dynatrace.OpenTelemetry.Exporter;
using Microsoft.Extensions.Logging;

namespace Dynatrace.OpenTelemetry.Exporter
{
    /// <summary>
    /// Queries the OneAgent to get metadata about the current process and enriches metric labels with them.
    /// </summary>
    public class OneAgentMetadataEnricher
    {
        private readonly ILogger<DynatraceMetricsExporter> logger;

        public OneAgentMetadataEnricher(ILogger<DynatraceMetricsExporter> logger)
        {
            this.logger = logger;
        }

        public void EnrichWithDynatraceMetadata(ICollection<KeyValuePair<string, string>> labels)
        {
            var metadata = ReadOneAgentMetadata(GetMetadataFileContent());
            foreach (var md in metadata)
            {
                labels.Add(new KeyValuePair<string, string>(md.Key, md.Value));
            }
        }

        public IEnumerable<KeyValuePair<string, string>> ReadOneAgentMetadata(string[] lines)
        {
            foreach (var line in lines)
            {
                logger.LogDebug("Parsing OneAgent metadata file: {Line}", line);
                var split = line.Split('=');
                if (split.Length != 2)
                {
                    logger.LogWarning("Failed to parse line from OneAgent metadata file: {Line}", line);
                    continue;
                }
                var key = split[0];
                var value = split[1];
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                {
                    logger.LogWarning("Failed to parse line from OneAgent metadata file: {Line}", line);
                    continue;
                }
                yield return new KeyValuePair<string, string>(split[0], split[1]);
            }
        }

        private string[] GetMetadataFileContent()
        {
            try
            {
                var metadataFilePath = File.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties");
                if (string.IsNullOrEmpty(metadataFilePath)) return Array.Empty<string>();
                return File.ReadAllLines(metadataFilePath);
            }
            catch (Exception e)
            {
                logger.LogDebug("Could not read OneAgent metadata file. This is normal if OneAgent is not installed. Message: {Message}", e.Message);
                return Array.Empty<string>();
            }
        }
    }
}
