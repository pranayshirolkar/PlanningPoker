using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Slack.NetStandard;
using Slack.NetStandard.Interaction;
using Slack.NetStandard.Messages.Blocks;
using Slack.NetStandard.Messages.Elements;

namespace PlanningPoker
{
    public static class MessageHelpers
    {
        public static InteractionMessage CreateDealtMessage(string username, string dealItem,
            IList<UserGroup> userGroups)
        {
            var message = new InteractionMessage(ResponseType.InChannel);
            message.Blocks.Add(new Section()
            {
                Text = new MarkdownText("@" + username + " dealt a hand. Please vote for *" + dealItem + "*." +
                                        (userGroups.Any()
                                            ? Environment.NewLine +
                                              "Groups: " + string.Join(", ",
                                                  userGroups.Select(g => "@" + g.UserGroupHandle))
                                            : "")),
                Accessory = new Button()
                {
                    Text = "Close and Reveal votes",
                    Style = ButtonStyle.Primary,
                    Value = Constants.CloseVote
                }
            });
            message.Blocks.Add(new Divider());
            message.Blocks.Add(new Actions()
            {
                BlockId = "dealingBlockId",
                Elements = new List<IMessageElement>()
                {
                    new Button()
                    {
                        Text = "1",
                        Value = "1"
                    },
                    new Button()
                    {
                        Text = "2",
                        Value = "2"
                    },
                    new Button()
                    {
                        Text = "3",
                        Value = "3"
                    },
                    new Button()
                    {
                        Text = "5",
                        Value = "5"
                    },
                    new Button()
                    {
                        Text = "8",
                        Value = "8"
                    },
                    new Button()
                    {
                        Text = "13",
                        Value = "13"
                    },
                    new Button()
                    {
                        Text = "21",
                        Value = "21"
                    },
                    new Button()
                    {
                        Text = "34",
                        Value = "34"
                    },
                    new Button()
                    {
                        Text = "?",
                        Value = "?"
                    }
                }
            });
            message.Blocks.Add(new Divider());
            message.Blocks.Add(new Section()
            {
                Text = new MarkdownText(Constants.NoVotesYet)
            });
            return message;
        }

        public static InteractionMessage GetMessageWithNewVoteAdded(IList<IMessageBlock> blocks,
            IList<string> usersVoted)
        {
            var responseMessage = new InteractionMessage(true);
            responseMessage.Blocks = blocks;
            var block = new Section()
            {
                Text = new MarkdownText(usersVoted.Count + " Voted: " +
                                        string.Join(", ", usersVoted.Select(u => "@" + u)))
            };

            responseMessage.Blocks[^1] = block;
            return responseMessage;
        }

        public static InteractionMessage GetMessageWithVotesClosed(IList<IMessageBlock> blocks,
            List<UserGroupWithUsers> setOfGroups,
            IDictionary<string, Vote> results,
            string username)
        {
            var sb = new StringBuilder();

            if (setOfGroups.Any())
            {
                foreach (var userGroup in setOfGroups)
                {
                    sb.Append("Votes from @" + userGroup.UserGroupHandle + ":");
                    sb.Append(Environment.NewLine);
                    HandleOutput(results, userGroup.UserIds, sb);
                }
            }
            else
            {
                HandleOutput(results, null, sb);
            }

            var message = new InteractionMessage(replaceOriginal: true);
            message.Blocks = blocks;
            message.Blocks[0] = new Section()
            {
                Text = new MarkdownText(((Section) blocks[0]).Text.Text)
            };
            message.Blocks[^3] = new Section()
            {
                Text = new MarkdownText("Got " + results.Count + " Votes." + " Closed by @" + username + " on " +
                                        "<!date^" +
                                        DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds.ToString().Split('.')[
                                            0] +
                                        "^{date_short} at {time}|unknown date time>")
            };
            message.Blocks[^1] = new Section()
            {
                Text = new MarkdownText(!results.Any() ? "No one voted." : sb.ToString())
            };
            return message;
        }

        private static void HandleOutput(IDictionary<string, Vote> results, IList<string> userGroup, StringBuilder sb)
        {
            var resultsGroupedByVoteValue =
                (userGroup == null ? results : results.Where(r => userGroup.Contains(r.Key)))
                .OrderBy(result => result.Value.Value)
                .GroupBy(result => result.Value.Value);
            foreach (var resultGroup in resultsGroupedByVoteValue)
            {
                sb.Append("```" + resultGroup.Key + ": ");
                foreach (var result in resultGroup)
                {
                    sb.Append("@" + result.Value.Username + ", ");
                }

                sb.Remove(sb.Length - 2, 1);
                sb.Append("```");
                sb.Append(Environment.NewLine);
            }
        }

        public static InteractionMessage CreateEphemeralMessage(string text)
        {
            var message = new InteractionMessage(ResponseType.Ephemeral);
            message.Blocks.Add(new Section()
                {
                    Text = new MarkdownText(text)
                }
            );
            return message;
        }
    }
}