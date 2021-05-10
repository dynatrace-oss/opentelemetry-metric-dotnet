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

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests
{
    public class OneAgentMetadataEnricherTests
    {
        [Fact]
        public void ProcessGroupOkMultiline()
        {
            var enricher = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance);
            var metadata = enricher.ReadOneAgentMetadata(new string[] {
                "a=123",
                "b=456",
            });
            Assert.Collection(metadata,
              elem1 =>
              {
                  Assert.Equal("a", elem1.Key);
                  Assert.Equal("123", elem1.Value);
              },
              elem2 =>
              {
                  Assert.Equal("b", elem2.Key);
                  Assert.Equal("456", elem2.Value);
              });
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
