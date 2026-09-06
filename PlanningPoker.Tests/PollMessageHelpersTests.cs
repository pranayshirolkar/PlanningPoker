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
        public void BuildInitialBlocks_has_one_button_carrying_the_roster()
        {
            var blocks = PollMessageHelpers.BuildInitialBlocks("Validated?", Roster);

            // Manual close was retired: confirming is the only action on the message.
            Assert.Null(((Section) blocks[0]).Accessory);
            var confirmButton = (Button) ((Actions) blocks[2]).Elements.Single();
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
        [InlineData("someAction|U1", "someAction", new[] { "U1" })]
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
        public void ParseConfirmedFromBlocks_round_trips_a_completed_poll()
        {
            // A late duplicate click reconstructs from the completed message. If this stopped
            // returning the full roster, that click would reopen the finished poll at 1/N.
            var completed = PollMessageHelpers.BuildCompletedUpdate(
                PollMessageHelpers.BuildInitialBlocks("Validated?", Roster), Roster);

            Assert.Equal(new HashSet<string>(Roster),
                PollMessageHelpers.ParseConfirmedFromBlocks(completed.Blocks));
        }

        [Fact]
        public void ParseConfirmedFromBlocks_reads_the_confirmed_line_not_the_first_line()
        {
            // Legacy layout: a poll closed by the retired button carries a header above the confirmed
            // line. Reading line 0 there parsed the closer as the only confirmer and dropped the rest.
            var blocks = new List<IMessageBlock>
            {
                new Section
                {
                    Text = new MarkdownText("*2/3 confirmed.* Closed by <@U9>.\n"
                                            + ":white_check_mark: *Confirmed*: <@U1> <@U2>\n"
                                            + ":hourglass_flowing_sand: *Pending*: <@U3>")
                }
            };

            Assert.Equal(new HashSet<string> { "U1", "U2" },
                PollMessageHelpers.ParseConfirmedFromBlocks(blocks));
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
        public void BuildCompletedUpdate_drops_the_button_and_summarises()
        {
            var blocks = PollMessageHelpers.BuildInitialBlocks("Validated?", Roster);
            var completed = PollMessageHelpers.BuildCompletedUpdate(blocks, Roster);

            Assert.DoesNotContain(completed.Blocks, b => b is Actions);
            Assert.Null(((Section) completed.Blocks[0]).Accessory);
            Assert.Equal("*Validated?*", ((Section) completed.Blocks[0]).Text.Text);

            var summary = ((Section) completed.Blocks[^1]).Text.Text;
            Assert.Contains("*Confirmed by all 3*", summary);
            Assert.Contains("<@U1> <@U2> <@U3>", summary);
            Assert.DoesNotContain("Pending", summary);
        }

        [Fact]
        public void BuildCompletedUpdate_is_idempotent()
        {
            // A duplicate or reordered click re-renders a finished poll; the result must not drift.
            var once = PollMessageHelpers.BuildCompletedUpdate(
                PollMessageHelpers.BuildInitialBlocks("Validated?", Roster), Roster);
            var twice = PollMessageHelpers.BuildCompletedUpdate(once.Blocks, Roster);

            Assert.Equal(((Section) once.Blocks[0]).Text.Text, ((Section) twice.Blocks[0]).Text.Text);
            Assert.Equal(((Section) once.Blocks[^1]).Text.Text, ((Section) twice.Blocks[^1]).Text.Text);
        }
    }
}
