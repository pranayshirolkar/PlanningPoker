using System.Collections.Generic;
using System.Linq;
using PlanningPoker;
using Slack.NetStandard;
using Slack.NetStandard.Messages.Blocks;
using Slack.NetStandard.Messages.Elements;
using Xunit;

namespace PlanningPoker.Tests
{
    public class PollMessageHelpersTests
    {
        private static readonly List<string> Roster = new() { "U1", "U2", "U3" };

        [Fact]
        public void BuildInitialBlocks_embeds_roster_in_both_button_values()
        {
            var blocks = PollMessageHelpers.BuildInitialBlocks("Validated?", Roster);

            var closeButton = (Button) ((Section) blocks[0]).Accessory;
            var confirmButton = (Button) ((Actions) blocks[2]).Elements.Single();

            Assert.Equal("pollClose|U1,U2,U3", closeButton.Value);
            Assert.Equal("pollConfirm|U1,U2,U3", confirmButton.Value);
        }

        [Fact]
        public void BuildInitialBlocks_starts_with_zero_confirmed_and_all_pending()
        {
            var blocks = PollMessageHelpers.BuildInitialBlocks("Validated?", Roster);
            var tally = ((Section) blocks[^1]).Text.Text;

            Assert.Contains("*Confirmed 0/3*", tally);
            Assert.Contains("<@U1>", tally);
            Assert.Contains("<@U2>", tally);
            Assert.Contains("<@U3>", tally);
        }

        [Fact]
        public void BuildTallySection_splits_confirmed_and_pending()
        {
            var tally = PollMessageHelpers.BuildTallySection(Roster, new HashSet<string> { "U1" }).Text.Text;
            var lines = tally.Split('\n');

            Assert.Contains("*Confirmed 1/3*", lines[0]);
            Assert.Contains("<@U1>", lines[0]);
            Assert.DoesNotContain("<@U2>", lines[0]);
            Assert.Contains("<@U2>", lines[1]);
            Assert.Contains("<@U3>", lines[1]);
        }

        [Theory]
        [InlineData("pollConfirm|U1,U2", "pollConfirm", new[] { "U1", "U2" })]
        [InlineData("pollClose|U1", "pollClose", new[] { "U1" })]
        [InlineData("pollConfirm|", "pollConfirm", new string[0])]
        [InlineData("closeVote", "closeVote", new string[0])]
        [InlineData("", "", new string[0])]
        public void ParseActionValue_splits_action_and_roster(string value, string action, string[] roster)
        {
            var (parsedAction, parsedRoster) = PollMessageHelpers.ParseActionValue(value);

            Assert.Equal(action, parsedAction);
            Assert.Equal(roster, parsedRoster.ToArray());
        }

        [Fact]
        public void ParseConfirmedFromBlocks_round_trips_the_confirmed_set()
        {
            // Simulate a rendered message after two confirmations, then reconstruct.
            var blocks = PollMessageHelpers.BuildInitialBlocks("Validated?", Roster);
            blocks[^1] = PollMessageHelpers.BuildTallySection(Roster, new HashSet<string> { "U1", "U3" });

            var reconstructed = PollMessageHelpers.ParseConfirmedFromBlocks(blocks);

            Assert.Equal(new HashSet<string> { "U1", "U3" }, reconstructed);
        }

        [Fact]
        public void ParseConfirmedFromBlocks_ignores_pending_mentions()
        {
            var blocks = new List<IMessageBlock>
            {
                PollMessageHelpers.BuildTallySection(Roster, new HashSet<string> { "U2" })
            };

            var reconstructed = PollMessageHelpers.ParseConfirmedFromBlocks(blocks);

            // Only U2 is confirmed; U1/U3 are pending and must not be parsed as confirmed.
            Assert.Equal(new HashSet<string> { "U2" }, reconstructed);
        }

        [Fact]
        public void ParseConfirmedFromBlocks_handles_enterprise_W_ids()
        {
            var roster = new List<string> { "W111", "U222" };
            var blocks = new List<IMessageBlock>
            {
                PollMessageHelpers.BuildTallySection(roster, new HashSet<string> { "W111" })
            };

            Assert.Equal(new HashSet<string> { "W111" }, PollMessageHelpers.ParseConfirmedFromBlocks(blocks));
        }

        [Fact]
        public void ParseTimestamp_splits_epoch_and_identifier()
        {
            var ts = PollMessageHelpers.ParseTimestamp("1720531234.567890");

            Assert.Equal(1720531234, ts.EpochSeconds);
            Assert.Equal("567890", ts.Identifier);
            Assert.Equal("1720531234.567890", ts.ToString());
        }

        [Fact]
        public void BuildClosedUpdate_removes_buttons_and_shows_summary()
        {
            var blocks = PollMessageHelpers.BuildInitialBlocks("Validated?", Roster);
            var closed = PollMessageHelpers.BuildClosedUpdate(blocks, Roster,
                new HashSet<string> { "U1", "U2", "U3" }, closedByUserId: "U9");

            Assert.DoesNotContain(closed.Blocks, b => b is Actions);
            Assert.Null(((Section) closed.Blocks[0]).Accessory);
            Assert.Contains("3/3 confirmed", ((Section) closed.Blocks[^1]).Text.Text);
            Assert.Contains("<@U9>", ((Section) closed.Blocks[^1]).Text.Text);
        }

        [Fact]
        public void BuildClosedUpdate_autoclose_says_everyone()
        {
            var blocks = PollMessageHelpers.BuildInitialBlocks("Validated?", Roster);
            var closed = PollMessageHelpers.BuildClosedUpdate(blocks, Roster,
                new HashSet<string> { "U1", "U2", "U3" }, closedByUserId: null);

            Assert.Contains("everyone", ((Section) closed.Blocks[^1]).Text.Text);
        }
    }
}
