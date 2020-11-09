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
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace OpenTelemetry.Exporter.Dynatrace.Metrics.Tests
{
    public class OneAgentMetadataEnricherTests
    {
        [Fact]
        public void ProcessGroupOk()
        {
            var enricher = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance);
            var metadata = enricher.ReadOneAgentMetadata(new string[] { "processGroupInstance=0x5c14d9a68d569861" });
            Assert.Single(metadata);
            Assert.Equal("dt.entity.process_group_instance", metadata.Single().Key);
            Assert.Equal("PROCESS_GROUP_INSTANCE-5C14D9A68D569861", metadata.Single().Value);
        }

        [Fact]
        public void ProcessGroupOkMultiline()
        {
            var enricher = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance);
            var metadata = enricher.ReadOneAgentMetadata(new string[] {
                "processGroupInstance=0x5c14d9a68d569861",
                "otherKey=1234",
            });
            Assert.Single(metadata);
            Assert.Equal("dt.entity.process_group_instance", metadata.Single().Key);
            Assert.Equal("PROCESS_GROUP_INSTANCE-5C14D9A68D569861", metadata.Single().Value);
        }

        [Fact]
        public void WrongKey()
        {
            var enricher = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance);
            var metadata = enricher.ReadOneAgentMetadata(new string[] { "otherKey=0x5c14d9a68d569861" });
            Assert.Empty(metadata);
        }

        [Fact]
        public void WrongSyntax()
        {
            var enricher = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance);
            Assert.Empty(enricher.ReadOneAgentMetadata(new string[] { "=0x5c14d9a68d569861" }));
            Assert.Empty(enricher.ReadOneAgentMetadata(new string[] { "otherKey=" }));
            Assert.Empty(enricher.ReadOneAgentMetadata(new string[] { "" }));
            Assert.Empty(enricher.ReadOneAgentMetadata(new string[] { "=" }));
            Assert.Empty(enricher.ReadOneAgentMetadata(new string[] { "===" }));
            Assert.Empty(enricher.ReadOneAgentMetadata(new string[] { }));
        }
    }
}
