using PlanningPoker;
using Xunit;

namespace PlanningPoker.Tests
{
    public class SlashPollParserTests
    {
        [Fact]
        public void Parse_extracts_question_and_users()
        {
            var (question, userIds, groupIds) =
                SlashPollParser.Parse("\"Validated?\" <@U123|alice> <@W456|bob>");

            Assert.Equal("Validated?", question);
            Assert.Equal(new[] { "U123", "W456" }, userIds.ToArray());
            Assert.Empty(groupIds);
        }

        [Fact]
        public void Parse_extracts_usergroups()
        {
            var (_, userIds, groupIds) =
                SlashPollParser.Parse("\"Ready?\" <!subteam^S999|team> <@U1|a>");

            Assert.Equal(new[] { "U1" }, userIds.ToArray());
            Assert.Equal(new[] { "S999" }, groupIds.ToArray());
        }

        [Fact]
        public void Parse_returns_null_question_when_unquoted()
        {
            var (question, _, _) = SlashPollParser.Parse("no quotes here <@U1|a>");

            Assert.Null(question);
        }

        [Fact]
        public void Parse_dedupes_repeated_mentions()
        {
            var (_, userIds, _) = SlashPollParser.Parse("\"Q?\" <@U1|a> <@U1|a> <@U2|b>");

            Assert.Equal(new[] { "U1", "U2" }, userIds.ToArray());
        }

        [Fact]
        public void Parse_handles_empty_text()
        {
            var (question, userIds, groupIds) = SlashPollParser.Parse(null);

            Assert.Null(question);
            Assert.Empty(userIds);
            Assert.Empty(groupIds);
        }
    }
}
