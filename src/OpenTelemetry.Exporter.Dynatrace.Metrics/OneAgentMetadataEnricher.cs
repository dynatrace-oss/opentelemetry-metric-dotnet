using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Exporter.Dynatrace.Metrics
{
    public class OneAgentMetadataEnricher
    {
        private readonly ILogger<DynatraceMetricsExporter> logger;

        public OneAgentMetadataEnricher(ILogger<DynatraceMetricsExporter> logger)
        {
            this.logger = logger;
        }

        public void EnrichWithDynatraceMetadata(ICollection<KeyValuePair<string, string>> labels)
        {
            var metadata = ReadOneAgentMetadata(GetMagicFileContent());
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
                if (split[0] == "processGroupInstance")
                    yield return new KeyValuePair<string, string>("dt.entity.process_group_instance", ToEntityId(split[1], "PROCESS_GROUP_INSTANCE"));
            }
        }

        /// <summary>
        /// Converts metadata read from OneAgent to Monitored Entity IDs
        /// </summary>
        /// <param name="entityIdOriginal">entity id as hex with leading '0x', e.g. '0x46f1121843b79f56'</param>
        /// <param name="entityPrefix">dynatrace entity type name, e.g. 'PROCESS_GROUP_INSTANCE' or 'HOST'</param>
        /// <returns>Entity ID as expected by metrics API. e.g. 'PROCESS_GROUP_INSTANCE-E0D8F94D9065F24F'</returns>
        private string ToEntityId(string entityIdOriginal, string entityPrefix)
        {
            string entityId = entityIdOriginal.ToUpperInvariant();
            if (entityId.StartsWith("0X")) entityId = entityId.Substring(2);
            return $"{entityPrefix}-{entityId}";
        }

        private string[] GetMagicFileContent()
        {
            try
            {
                return File.ReadAllLines("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties");
            }
            catch (Exception e)
            {
                logger.LogDebug("Could not read OneAgent metadata file. This is normal if OneAgent is not installed. Message: {Message}", e.Message);
                return new string[0];
            }
        }
    }
}
