// <copyright company="Dynatrace LLC">
// Copyright 2020 Dynatrace LLC
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
using Microsoft.Extensions.Logging;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics
{
    /// <summary>
    /// Queries Dynatrace metadata, and provides it as key-value pairs.
    /// </summary>
    internal class DynatraceMetadataEnricher
    {
        private readonly ILogger<DynatraceMetricsExporter> _logger;
        private readonly IFileReader _fileReader;

        private const string OneAgentIndirectionFileName = "dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties";

        public DynatraceMetadataEnricher(ILogger<DynatraceMetricsExporter> logger) : this(logger, new DefaultFileReader()) { }

        // Allows mocking of File.ReadAllText and File.ReadAllLines methods. When using the public constructor,
        // the used FileReader passes the calls through to the System.IO methods.
        internal DynatraceMetadataEnricher(ILogger<DynatraceMetricsExporter> logger, IFileReader fileReader)
        {
            this._logger = logger;
            this._fileReader = fileReader;
        }

        public void EnrichWithDynatraceMetadata(ICollection<KeyValuePair<string, string>> labels)
        {
            var metadata = ProcessMetadata(GetMetadataFileContent());
            foreach (var md in metadata)
            {
                labels.Add(new KeyValuePair<string, string>(md.Key, md.Value));
            }
        }

        internal IEnumerable<KeyValuePair<string, string>> ProcessMetadata(string[] lines)
        {
            foreach (var line in lines)
            {
                _logger.LogDebug("Parsing metadata line: {Line}", line);
                var split = line.Split('=');
                if (split.Length != 2)
                {
                    _logger.LogWarning("Failed to parse line from metadata file: {Line}", line);
                    continue;
                }
                var key = split[0];
                var value = split[1];
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                {
                    _logger.LogWarning("Failed to parse line from metadata file: {Line}", line);
                    continue;
                }
                yield return new KeyValuePair<string, string>(split[0], split[1]);
            }
        }

        private string[] GetMetadataFileContent()
        {
            try
            {
                var metadataFilePath = _fileReader.ReadAllText(OneAgentIndirectionFileName);
                if (string.IsNullOrEmpty(metadataFilePath)) return Array.Empty<string>();
                return _fileReader.ReadAllLines(metadataFilePath);
            }
            catch (Exception e)
            {
                _logger.LogWarning("Could not read metadata file. This is normal if OneAgent is not installed.", e.Message);
                return Array.Empty<string>();
            }
        }
    }
}
