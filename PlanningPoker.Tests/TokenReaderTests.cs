using System;
using PlanningPoker;
using Xunit;

namespace PlanningPoker.Tests
{
    public class TokenReaderTests
    {
        private static readonly string[] Single = { "T111:xoxb-single" };
        private static readonly string[] Multi = { "T111:xoxb-one", "T222:xoxb-two" };

        [Fact]
        public void ResolveToken_matches_by_team_id()
        {
            Assert.Equal("xoxb-two", TokenReader.ResolveToken(Multi, "T222"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ResolveToken_falls_back_to_sole_token_when_no_team_id(string teamId)
        {
            Assert.Equal("xoxb-single", TokenReader.ResolveToken(Single, teamId));
        }

        [Fact]
        public void ResolveToken_throws_when_no_team_id_but_multiple_configured()
        {
            // Ambiguous: a multi-workspace config must supply a team_id.
            Assert.Throws<InvalidOperationException>(() => TokenReader.ResolveToken(Multi, null));
        }

        [Fact]
        public void ResolveToken_preserves_tokens_containing_the_separator()
        {
            Assert.Equal("xoxb:with:colons", TokenReader.ResolveToken(new[] { "T111:xoxb:with:colons" }, "T111"));
        }
    }
}
