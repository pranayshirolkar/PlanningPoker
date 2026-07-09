using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Slack.NetStandard;
using Slack.NetStandard.Interaction;
using Slack.NetStandard.Messages.Blocks;
using Slack.NetStandard.Messages.Elements;

namespace PlanningPoker
{
    // Pure building/parsing for confirmation polls. No IO — the network send (response_url /
    // chat.postMessage) stays in PollService so this layer is unit-testable.
    public static class PollMessageHelpers
    {
        // Slack renders <@USERID> as the display name, so everything is keyed on user IDs and we
        // never fetch usernames. IDs are U… (or W… on Enterprise Grid).
        private static readonly Regex MentionRegex = new(@"<@([UW][A-Z0-9]+)>");

        public static List<IMessageBlock> BuildInitialBlocks(string question, IReadOnlyList<string> roster)
        {
            var rosterCsv = string.Join(",", roster);
            return new List<IMessageBlock>
            {
                new Section
                {
                    Text = new MarkdownText("*" + question + "*"),
                    Accessory = new Button
                    {
                        Text = new PlainText("Close & Reveal"),
                        Style = ButtonStyle.Danger,
                        Value = Constants.PollCloseAction + Constants.PollValueSeparator + rosterCsv
                    }
                },
                new Divider(),
                new Actions
                {
                    BlockId = "pollActions",
                    Elements = new List<IMessageElement>
                    {
                        new Button
                        {
                            Text = new PlainText(":white_check_mark: Confirm"),
                            Style = ButtonStyle.Primary,
                            Value = Constants.PollConfirmAction + Constants.PollValueSeparator + rosterCsv
                        }
                    }
                },
                new Divider(),
                BuildTallySection(roster, new HashSet<string>())
            };
        }

        public static Section BuildTallySection(IReadOnlyList<string> roster, ISet<string> confirmed)
        {
            // Confirmed and pending are rendered in roster order for a stable display across updates.
            var confirmedMentions = roster.Where(confirmed.Contains).Select(Mention).ToList();
            var pendingMentions = roster.Where(id => !confirmed.Contains(id)).Select(Mention).ToList();

            var confirmedLine = $":white_check_mark: *Confirmed {confirmedMentions.Count}/{roster.Count}*"
                                + (confirmedMentions.Any() ? ": " + string.Join(" ", confirmedMentions) : "");
            var pendingLine = ":hourglass_flowing_sand: *Pending*"
                              + (pendingMentions.Any() ? ": " + string.Join(" ", pendingMentions) : ": none :tada:");

            return new Section { Text = new MarkdownText(confirmedLine + "\n" + pendingLine) };
        }

        public static InteractionMessage BuildActiveUpdate(IList<IMessageBlock> existingBlocks,
            IReadOnlyList<string> roster, ISet<string> confirmed)
        {
            // Only the tally (last block) changes on a vote — question + buttons are untouched.
            var message = new InteractionMessage(replaceOriginal: true) { Blocks = existingBlocks };
            message.Blocks[^1] = BuildTallySection(roster, confirmed);
            return message;
        }

        public static InteractionMessage BuildClosedUpdate(IList<IMessageBlock> existingBlocks,
            IReadOnlyList<string> roster, ISet<string> confirmed, string closedByUserId)
        {
            var message = new InteractionMessage(replaceOriginal: true) { Blocks = existingBlocks };
            // Drop the accessory (Close button) from the question section.
            message.Blocks[0] = new Section { Text = new MarkdownText(((Section) existingBlocks[0]).Text.Text) };
            var closedBy = closedByUserId != null ? Mention(closedByUserId) : "everyone";
            var header = $"*{confirmed.Count(roster.Contains)}/{roster.Count} confirmed.* Closed by {closedBy}.";
            message.Blocks = new List<IMessageBlock>
            {
                message.Blocks[0],
                new Divider(),
                new Section { Text = new MarkdownText(header + "\n" + TallyLines(roster, confirmed)) }
            };
            return message;
        }

        public static (string action, List<string> roster) ParseActionValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return (string.Empty, new List<string>());
            }

            var sep = value.IndexOf(Constants.PollValueSeparator);
            if (sep < 0)
            {
                return (value, new List<string>());
            }

            var action = value.Substring(0, sep);
            var rosterCsv = value.Substring(sep + 1);
            var roster = rosterCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => id.Trim())
                .Where(id => id.Length > 0)
                .ToList();
            return (action, roster);
        }

        // Best-effort reconstruction of the confirmed set from the rendered message, used only on a
        // cold-start state miss (the in-memory store is authoritative on the warm path). Parsed from
        // the tally text rather than block_id because a large confirmed set would overflow block_id's
        // 255-char cap; section text (3000) has room.
        public static HashSet<string> ParseConfirmedFromBlocks(IList<IMessageBlock> blocks)
        {
            var confirmed = new HashSet<string>();
            if (blocks == null)
            {
                return confirmed;
            }

            var tally = blocks.OfType<Section>().LastOrDefault(s => s.Text?.Text != null
                && s.Text.Text.Contains("*Confirmed"));
            if (tally == null)
            {
                return confirmed;
            }

            // Only the first line (the "Confirmed" line) carries confirmed mentions; pending is line 2.
            var confirmedLine = tally.Text.Text.Split('\n')[0];
            foreach (Match m in MentionRegex.Matches(confirmedLine))
            {
                confirmed.Add(m.Groups[1].Value);
            }

            return confirmed;
        }

        // thread_ts arrives as "epochSeconds.identifier"; Timestamp has no string ctor.
        public static Timestamp ParseTimestamp(string ts)
        {
            var parts = ts.Split('.');
            var epoch = long.Parse(parts[0]);
            var identifier = parts.Length > 1 ? parts[1] : "000000";
            return new Timestamp(epoch, identifier);
        }

        private static string Mention(string userId) => $"<@{userId}>";

        private static string TallyLines(IReadOnlyList<string> roster, ISet<string> confirmed)
        {
            var confirmedMentions = roster.Where(confirmed.Contains).Select(Mention).ToList();
            var pendingMentions = roster.Where(id => !confirmed.Contains(id)).Select(Mention).ToList();
            var confirmedLine = ":white_check_mark: *Confirmed*"
                                + (confirmedMentions.Any() ? ": " + string.Join(" ", confirmedMentions) : ": none");
            var pendingLine = ":hourglass_flowing_sand: *Pending*"
                              + (pendingMentions.Any() ? ": " + string.Join(" ", pendingMentions) : ": none :tada:");
            return confirmedLine + "\n" + pendingLine;
        }
    }
}
