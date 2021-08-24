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

using System.Linq;
using Xunit;

// This warning appears since the parametrized unit tests have a name (for human readability)
// which is not used, since it is currently not supported by xUnit. This #pragma disables the warning that the name is not used.
#pragma warning disable xUnit1026

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Utils.Tests
{

    public class NormalizeTests
    {
        [Theory]
        [InlineData("valid base case", "basecase", "basecase")]
        [InlineData("valid base case", "just.a.normal.key", "just.a.normal.key")]
        [InlineData("valid leading underscore", "_case", "_case")]
        [InlineData("valid underscore", "case_case", "case_case")]
        [InlineData("valid number", "case1", "case1")]
        [InlineData("invalid leading number", "1case", "_case")]
        [InlineData("invalid multiple leading", "!@#case", "_case")]
        [InlineData("invalid multiple trailing", "case!@#", "case_")]
        [InlineData("valid leading uppercase", "Case", "Case")]
        [InlineData("valid all uppercase", "CASE", "CASE")]
        [InlineData("valid intermittent uppercase", "someCase", "someCase")]
        [InlineData("valid multiple sections", "prefix.case", "prefix.case")]
        [InlineData("valid multiple sections upper", "This.Is.Valid", "This.Is.Valid")]
        [InlineData("invalid multiple sections leading number", "0a.b", "_a.b")]
        [InlineData("valid multiple section leading underscore", "_a.b", "_a.b")]
        [InlineData("valid leading number second section", "a.0", "a.0")]
        [InlineData("valid leading number second section 2", "a.0.c", "a.0.c")]
        [InlineData("valid leading number second section 3", "a.0b.c", "a.0b.c")]
        [InlineData("invalid leading hyphen", "-dim", "_dim")]
        [InlineData("valid trailing hyphen", "dim-", "dim-")]
        [InlineData("valid trailing hyphens", "dim---", "dim---")]
        [InlineData("invalid empty", "", null)]
        [InlineData("invalid only number", "000", "_")]
        [InlineData("invalid key first section only number", "0.section", "_.section")]
        [InlineData("invalid leading character", "~key", "_key")]
        [InlineData("invalid leading characters", "~0#key", "_key")]
        [InlineData("invalid intermittent character", "some~key", "some_key")]
        [InlineData("invalid intermittent characters", "some#~äkey", "some_key")]
        [InlineData("invalid two consecutive dots", "a..b", "a.b")]
        [InlineData("invalid five consecutive dots", "a.....b", "a.b")]
        [InlineData("invalid just a dot", ".", null)]
        [InlineData("invalid three dots", "...", null)]
        [InlineData("invalid leading dot", ".a", null)]
        [InlineData("invalid trailing dot", "a.", "a")]
        [InlineData("invalid enclosing dots", ".a.", null)]
        [InlineData("valid consecutive leading underscores", "___a", "___a")]
        [InlineData("valid consecutive trailing underscores", "a___", "a___")]
        [InlineData("invalid trailing invalid chars groups", "a.b$%@.c#@", "a.b_.c_")]
        [InlineData("valid consecutive enclosed underscores", "a___b", "a___b")]
        [InlineData("invalid mixture dots underscores", "._._._a_._._.", null)]
        [InlineData("valid mixture dots underscores 2", "_._._.a_._", "_._._.a_._")]
        [InlineData("invalid empty section", "an..empty.section", "an.empty.section")]
        [InlineData("invalid characters", "a,,,b  c=d\\e\\ =,f", "a_b_c_d_e_f")]
        [InlineData("invalid characters long", "a!b\"c#d$e%f&g'h(i)j*k+l,m-n.o/p:q;r<s=t>u?v@w[x]y\\z^0 1_2;3{4|5}6~7", "a_b_c_d_e_f_g_h_i_j_k_l_m-n.o_p_q_r_s_t_u_v_w_x_y_z_0_1_2_3_4_5_6_7")]
        [InlineData("invalid trailing characters", "a.b.+", "a.b._")]
        [InlineData("valid combined test", "metric.key-number-1.001", "metric.key-number-1.001")]
        [InlineData("valid example 1", "MyMetric", "MyMetric")]
        [InlineData("invalid example 1", "0MyMetric", "_MyMetric")]
        [InlineData("invalid example 2", "mÄtric", "m_tric")]
        [InlineData("invalid example 3", "metriÄ", "metri_")]
        [InlineData("invalid example 4", "Ätric", "_tric")]
        [InlineData("invalid example 5", "meträääääÖÖÖc", "metr_c")]
        public void MetricKeyNormalizedCorrectly(string name, string input, string expected)
        {
            Assert.Equal(expected, Normalize.MetricKey(input));
        }

        [Fact]
        public void MetricKeyTruncatedCorrectly()
        {
            Assert.Equal(new string('a', 250), Normalize.MetricKey(new string('a', 270)));
        }

        [Theory]
        [InlineData("valid case", "dim", "dim")]
        [InlineData("valid number", "dim1", "dim1")]
        [InlineData("valid leading underscore", "_dim", "_dim")]
        [InlineData("invalid leading uppercase", "Dim", "dim")]
        [InlineData("invalid internal uppercase", "dIm", "dim")]
        [InlineData("invalid trailing uppercase", "diM", "dim")]
        [InlineData("invalid leading umlaut and uppercase", "äABC", "_abc")]
        [InlineData("invalid multiple leading", "!@#case", "_case")]
        [InlineData("invalid multiple trailing", "case!@#", "case_")]
        [InlineData("invalid all uppercase", "DIM", "dim")]
        [InlineData("valid dimension colon", "dim:dim", "dim:dim")]
        [InlineData("valid dimension underscore", "dim_dim", "dim_dim")]
        [InlineData("valid dimension hyphen", "dim-dim", "dim-dim")]
        [InlineData("invalid leading hyphen", "-dim", "_dim")]
        [InlineData("valid trailing hyphen", "dim-", "dim-")]
        [InlineData("valid trailing hyphens", "dim---", "dim---")]
        [InlineData("invalid leading multiple hyphens", "---dim", "_dim")]
        [InlineData("invalid leading colon", ":dim", "_dim")]
        [InlineData("invalid chars", "~@#ä", "_")]
        [InlineData("invalid trailing chars", "aaa~@#ä", "aaa_")]
        [InlineData("valid trailing underscores", "aaa___", "aaa___")]
        [InlineData("invalid only numbers", "000", "_")]
        [InlineData("valid compound key", "dim1.value1", "dim1.value1")]
        [InlineData("invalid compound leading number", "dim.0dim", "dim._dim")]
        [InlineData("invalid compound only number", "dim.000", "dim._")]
        [InlineData("invalid compound leading invalid char", "dim.~val", "dim._val")]
        [InlineData("invalid compound trailing invalid char", "dim.val~~", "dim.val_")]
        [InlineData("invalid compound only invalid char", "dim.~~~", "dim._")]
        [InlineData("valid compound leading underscore", "dim._val", "dim._val")]
        [InlineData("valid compound only underscore", "dim.___", "dim.___")]
        [InlineData("valid compound long", "dim.dim.dim.dim", "dim.dim.dim.dim")]
        [InlineData("invalid two dots", "a..b", "a.b")]
        [InlineData("invalid five dots", "a.....b", "a.b")]
        [InlineData("invalid leading dot", ".a", "a")]
        [InlineData("valid colon in compound", "a.b:c.d", "a.b:c.d")]
        [InlineData("invalid trailing dot", "a.", "a")]
        [InlineData("invalid just a dot", ".", "")]
        [InlineData("invalid trailing dots", "a...", "a")]
        [InlineData("invalid enclosing dots", ".a.", "a")]
        [InlineData("invalid leading whitespace", "   a", "_a")]
        [InlineData("invalid trailing whitespace", "a   ", "a_")]
        [InlineData("invalid internal whitespace", "a b", "a_b")]
        [InlineData("invalid internal whitespace", "a    b", "a_b")]
        [InlineData("invalid empty", "", "")]
        [InlineData("valid combined key", "dim.val:count.val001", "dim.val:count.val001")]
        [InlineData("invalid characters", "a,,,b  c=d\\e\\ =,f", "a_b_c_d_e_f")]
        [InlineData("invalid characters long", "a!b\"c#d$e%f&g'h(i)j*k+l,m-n.o/p:q;r<s=t>u?v@w[x]y\\z^0 1_2;3{4|5}6~7", "a_b_c_d_e_f_g_h_i_j_k_l_m-n.o_p:q_r_s_t_u_v_w_x_y_z_0_1_2_3_4_5_6_7")]
        [InlineData("invalid example 1", "Tag", "tag")]
        [InlineData("invalid example 2", "0Tag", "_tag")]
        [InlineData("invalid example 3", "tÄg", "t_g")]
        [InlineData("invalid example 4", "mytäääg", "myt_g")]
        [InlineData("invalid example 5", "ääätag", "_tag")]
        [InlineData("invalid example 6", "ä_ätag", "___tag")]
        [InlineData("invalid example 7", "Bla___", "bla___")]
        public void DimensionKeyNormalizedCorrectly(string name, string input, string expected)
        {
            Assert.Equal(expected, Normalize.DimensionKey(input));
        }

        [Fact]
        public void DimensionKeyTruncatedCorrectly()
        {
            Assert.Equal(new string('a', 100), Normalize.DimensionKey(new string('a', 110)));
        }

        [Theory]
        [InlineData("valid value", "value", "value")]
        [InlineData("valid empty", "", "")]
        [InlineData("pass null", null, "")]
        [InlineData("valid uppercase", "VALUE", "VALUE")]
        [InlineData("valid colon", "a:3", "a:3")]
        [InlineData("valid value 2", "~@#ä", "~@#ä")]
        [InlineData("valid spaces", "a b", "a b")]
        [InlineData("valid comma", "a,b", "a,b")]
        [InlineData("valid equals", "a=b", "a=b")]
        [InlineData("valid backslash", "a\\b", "a\\b")]
        [InlineData("valid multiple special chars", " ,=\\", " ,=\\")]
        [InlineData("valid key-value pair", "key=\"value\"", "key=\"value\"")]
        //     \u0000 NUL character, \u0007 bell character
        [InlineData("invalid unicode", "\u0000a\u0007", "_a_")]
        [InlineData("invalid unicode space", "a\u0001b", "a_b")]
        //     'Ab' in unicode:
        [InlineData("valid unicode", "\u0034\u0066", "\u0034\u0066")]
        //     A umlaut, a with ring, O umlaut, U umlaut, all valid.
        [InlineData("valid unicode", "\u0132_\u0133_\u0150_\u0156", "\u0132_\u0133_\u0150_\u0156")]
        [InlineData("invalid leading unicode NUL", "\u0000a", "_a")]
        [InlineData("invalid only unicode", "\u0000\u0000", "_")]
        [InlineData("invalid consecutive leading unicode", "\u0000\u0000\u0000a", "_a")]
        [InlineData("invalid consecutive trailing unicode", "a\u0000\u0000\u0000", "a_")]
        [InlineData("invalid trailing unicode NUL", "a\u0000", "a_")]
        [InlineData("invalid enclosed unicode NUL", "a\u0000b", "a_b")]
        [InlineData("invalid consecutive enclosed unicode NUL", "a\u0000\u0007\u0000b", "a_b")]
        public void DimensionValueNormalizedCorrectly(string name, string input, string expected)
        {
            Assert.Equal(expected, Normalize.DimensionValue(input));
        }

        [Fact]
        public void DimensionValueTruncatedCorrectly()
        {
            Assert.Equal(new string('a', 250), Normalize.DimensionValue(new string('a', 270)));
        }

        [Theory]
        [InlineData("escape spaces", "a b", "a\\ b")]
        [InlineData("escape comma", "a,b", "a\\,b")]
        [InlineData("escape equals", "a=b", "a\\=b")]
        [InlineData("escape backslash", "a\\b", "a\\\\b")]
        [InlineData("escape double quotes", "a\"b\"\"c", "a\\\"b\\\"\\\"c")]
        [InlineData("escape multiple special chars", " ,=\\", "\\ \\,\\=\\\\")]
        [InlineData("escape consecutive special chars", "  ,,==\\\\", "\\ \\ \\,\\,\\=\\=\\\\\\\\")]
        [InlineData("escape key-value pair", "key=\"value\"", "key\\=\\\"value\\\"")]
        public void DimensionValueEscapedCorrectly(string name, string input, string expected)
        {
            Assert.Equal(expected, Normalize.EscapeDimensionValue(input));
        }

        [Fact]
        public void DimensionValueTruncatedCorrectlyAfterEscaping()
        {
            // escape too long string
            Assert.Equal(string.Concat(Enumerable.Repeat("\\=", 125)), Normalize.EscapeDimensionValue(new string('=', 250)));
            // escape sequence not broken apart 1
            Assert.Equal(new string('a', 249), Normalize.EscapeDimensionValue(new string('a', 249) + '='));
            //   escape sequence not broken apart 2
            Assert.Equal(new string('a', 248) + "\\=", Normalize.EscapeDimensionValue(new string('a', 248) + "=="));
            // escape sequence not broken apart 3:
            // 3 trailing backslashes before escaping, 1 escaped trailing backslash
            Assert.Equal(new string('a', 247) + "\\\\", Normalize.EscapeDimensionValue(new string('a', 247) + "\\\\\\"));
            // dimension value of only backslashes
            Assert.Equal(string.Concat(Enumerable.Repeat("\\\\", 125)), Normalize.EscapeDimensionValue(new string('\\', 260)));
        }
    }
}
