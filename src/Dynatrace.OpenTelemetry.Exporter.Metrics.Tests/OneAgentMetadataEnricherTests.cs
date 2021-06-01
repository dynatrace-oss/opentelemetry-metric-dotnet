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
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests
{
    public class OneAgentMetadataEnricherTests
    {
        [Fact]
        public void ValidMultiline()
        {
            var enricher = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance);
            var metadata = enricher.ProcessMetadata(new string[] {
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
            Assert.Empty(enricher.ProcessMetadata(new string[] { "=0x5c14d9a68d569861" }));
            Assert.Empty(enricher.ProcessMetadata(new string[] { "otherKey=" }));
            Assert.Empty(enricher.ProcessMetadata(new string[] { "" }));
            Assert.Empty(enricher.ProcessMetadata(new string[] { "=" }));
            Assert.Empty(enricher.ProcessMetadata(new string[] { "===" }));
            Assert.Empty(enricher.ProcessMetadata(new string[] { }));
        }

        [Fact]
        public void IndirectionFileMissing()
        {
            var fileReader = Mock.Of<IFileReader>();
            Mock.Get(fileReader).Setup(f => f.ReadAllText(It.IsAny<string>())).Throws<FileNotFoundException>();

            var kv = KeyValuePair.Create<string, string>("defaultKey", "defaultValue");
            var targetList = new List<KeyValuePair<string, string>>() { kv };

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, fileReader);

            unitUnderTest.EnrichWithDynatraceMetadata(targetList);
            // contains only the element that was in the list before.
            Assert.Collection(targetList, item => Assert.Equal(item, kv));
            Mock.Get(fileReader).Verify(mock => mock.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionFileNotAccessible()
        {
            var fileReader = Mock.Of<IFileReader>();
            Mock.Get(fileReader).Setup(f => f.ReadAllText(It.IsAny<string>())).Throws<UnauthorizedAccessException>();
            var kv = KeyValuePair.Create<string, string>("defaultKey", "defaultValue");
            var targetList = new List<KeyValuePair<string, string>>() { kv };

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, fileReader);
            unitUnderTest.EnrichWithDynatraceMetadata(targetList);

            Assert.Collection(targetList, item => Assert.Equal(item, kv));
            Mock.Get(fileReader).Verify(f => f.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionFileAnyOtherException()
        {
            var fileReader = Mock.Of<IFileReader>();
            // there is a whole host of exceptions that can be thrown by ReadAllText: https://docs.microsoft.com/en-us/dotnet/api/system.io.file.readalltext?view=net-5.0
            Mock.Get(fileReader).Setup(f => f.ReadAllText(It.IsAny<string>())).Throws<Exception>();
            var kv = KeyValuePair.Create<string, string>("defaultKey", "defaultValue");
            var targetList = new List<KeyValuePair<string, string>>() { kv };

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, fileReader);
            unitUnderTest.EnrichWithDynatraceMetadata(targetList);

            Assert.Collection(targetList, item => Assert.Equal(item, kv));
            Mock.Get(fileReader).Verify(f => f.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionFileEmpty()
        {
            var fileReader = Mock.Of<IFileReader>();
            Mock.Get(fileReader).Setup(f => f.ReadAllText(It.IsAny<string>())).Returns("");
            var kv = KeyValuePair.Create<string, string>("defaultKey", "defaultValue");
            var targetList = new List<KeyValuePair<string, string>>() { kv };

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, fileReader);
            unitUnderTest.EnrichWithDynatraceMetadata(targetList);

            Assert.Collection(targetList, item => Assert.Equal(item, kv));
            Mock.Get(fileReader).Verify(f => f.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            // if the OneAgent metadata file is empty, there should be no attempt at reading the contents.
            Mock.Get(fileReader).Verify(f => f.ReadAllLines(""), Times.Never());
        }

        [Fact]
        public void IndirectionFileContainsAdditionalText()
        {
            var fileReader = Mock.Of<IFileReader>();
            var indirectionFileContent =
            @"indirection_file_name.properties
            some other text
            and some more text";

            Mock.Get(fileReader).Setup(f => f.ReadAllText(It.IsAny<string>())).Returns(indirectionFileContent);
            var kv = KeyValuePair.Create<string, string>("defaultKey", "defaultValue");
            var targetList = new List<KeyValuePair<string, string>>() { kv };

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, fileReader);
            unitUnderTest.EnrichWithDynatraceMetadata(targetList);

            Assert.Collection(targetList, item => Assert.Equal(item, kv));
            Mock.Get(fileReader).Verify(f => f.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            // if the OneAgent metadata file is empty, there should be no attempt at reading the contents.
            Mock.Get(fileReader).Verify(f => f.ReadAllLines(indirectionFileContent), Times.Once());
        }

        [Fact]
        public void IndirectionTargetMissing()
        {
            var fileReader = Mock.Of<IFileReader>();
            Mock.Get(fileReader).Setup(f => f.ReadAllText(It.IsAny<string>())).Returns("indirection_file_name.properties");
            Mock.Get(fileReader).Setup(f => f.ReadAllLines(It.IsAny<string>())).Throws<FileNotFoundException>();
            var kv = KeyValuePair.Create<string, string>("defaultKey", "defaultValue");
            var targetList = new List<KeyValuePair<string, string>>() { kv };

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, fileReader);
            unitUnderTest.EnrichWithDynatraceMetadata(targetList);

            Assert.Collection(targetList, item => Assert.Equal(item, kv));
            Mock.Get(fileReader).Verify(f => f.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            Mock.Get(fileReader).Verify(f => f.ReadAllLines("indirection_file_name.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionTargetInvalidAccess()
        {
            var fileReader = Mock.Of<IFileReader>();
            Mock.Get(fileReader).Setup(f => f.ReadAllText(It.IsAny<string>())).Returns("indirection_file_name.properties");
            Mock.Get(fileReader).Setup(f => f.ReadAllLines(It.IsAny<string>())).Throws<AccessViolationException>();
            var kv = KeyValuePair.Create<string, string>("defaultKey", "defaultValue");
            var targetList = new List<KeyValuePair<string, string>>() { kv };

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, fileReader);
            unitUnderTest.EnrichWithDynatraceMetadata(targetList);

            Assert.Collection(targetList, item => Assert.Equal(item, kv));
            Mock.Get(fileReader).Verify(f => f.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            Mock.Get(fileReader).Verify(f => f.ReadAllLines("indirection_file_name.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionTargetThrowsAnyOtherException()
        {
            var fileReader = Mock.Of<IFileReader>();
            Mock.Get(fileReader).Setup(f => f.ReadAllText(It.IsAny<string>())).Returns("indirection_file_name.properties");
            Mock.Get(fileReader).Setup(f => f.ReadAllLines(It.IsAny<string>())).Throws<Exception>();
            var kv = KeyValuePair.Create<string, string>("defaultKey", "defaultValue");
            var targetList = new List<KeyValuePair<string, string>>() { kv };

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, fileReader);
            unitUnderTest.EnrichWithDynatraceMetadata(targetList);

            Assert.Collection(targetList, item => Assert.Equal(item, kv));
            Mock.Get(fileReader).Verify(f => f.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            Mock.Get(fileReader).Verify(f => f.ReadAllLines("indirection_file_name.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionTargetEmpty()
        {
            var fileReader = Mock.Of<IFileReader>();
            Mock.Get(fileReader).Setup(f => f.ReadAllText(It.IsAny<string>())).Returns("indirection_file_name.properties");
            Mock.Get(fileReader).Setup(f => f.ReadAllLines(It.IsAny<string>())).Returns(Array.Empty<string>());
            var kv = KeyValuePair.Create<string, string>("defaultKey", "defaultValue");
            var targetList = new List<KeyValuePair<string, string>>() { kv };

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, fileReader);
            unitUnderTest.EnrichWithDynatraceMetadata(targetList);

            Assert.Collection(targetList, item => Assert.Equal(item, kv));
            Mock.Get(fileReader).Verify(f => f.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            Mock.Get(fileReader).Verify(f => f.ReadAllLines("indirection_file_name.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionTargetValid()
        {
            var fileReader = Mock.Of<IFileReader>();
            Mock.Get(fileReader).Setup(f => f.ReadAllText(It.IsAny<string>())).Returns("indirection_file_name.properties");
            Mock.Get(fileReader).Setup(f => f.ReadAllLines(It.IsAny<string>())).Returns(new[] { "key1=value1", "key2=value2" });
            var kv = KeyValuePair.Create<string, string>("defaultKey", "defaultValue");
            var targetList = new List<KeyValuePair<string, string>>() { kv };

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, fileReader);
            unitUnderTest.EnrichWithDynatraceMetadata(targetList);

            Assert.Collection(targetList,
                item => Assert.Equal(kv, item),
                item => Assert.Equal(KeyValuePair.Create("key1", "value1"), item),
                item => Assert.Equal(KeyValuePair.Create("key2", "value2"), item)
            );
            Mock.Get(fileReader).Verify(f => f.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            Mock.Get(fileReader).Verify(f => f.ReadAllLines("indirection_file_name.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionTargetValidWithInvalidLines()
        {
            var fileReader = Mock.Of<IFileReader>();
            Mock.Get(fileReader).Setup(f => f.ReadAllText(It.IsAny<string>())).Returns("indirection_file_name.properties");
            Mock.Get(fileReader).Setup(f => f.ReadAllLines(It.IsAny<string>())).Returns(new[] { "key1=value1", "key2=", "=value2", "===" });
            var kv = KeyValuePair.Create<string, string>("defaultKey", "defaultValue");
            var targetList = new List<KeyValuePair<string, string>>() { kv };

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, fileReader);
            unitUnderTest.EnrichWithDynatraceMetadata(targetList);

            Assert.Collection(targetList,
                item => Assert.Equal(kv, item),
                item => Assert.Equal(KeyValuePair.Create("key1", "value1"), item)
            );
            Mock.Get(fileReader).Verify(f => f.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            Mock.Get(fileReader).Verify(f => f.ReadAllLines("indirection_file_name.properties"), Times.Once());
        }
    }
}
