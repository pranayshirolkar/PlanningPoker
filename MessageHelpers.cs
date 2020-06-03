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
        public static InteractionMessage CreateDealtMessage(string username, string dealItem)
        {
            var message = new InteractionMessage(ResponseType.InChannel);
            message.Blocks.Add(new Section()
            {
                Text = new MarkdownText("@" + username + " dealt " + dealItem),
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

        public static InteractionMessage GetMessageWithNewVoteAdded(IList<IMessageBlock> blocks, string username)
        {
            var responseMessage = new InteractionMessage(true);
            responseMessage.Blocks = blocks;
            var block = (Section) responseMessage.Blocks[^1];
            if (block.Text.Text.Equals(Constants.NoVotesYet))
            {
                block = new Section()
                {
                    Text = new MarkdownText("Voted: @" + username)
                };
            }
            else
            {
                block.Text.Text += ", @" + username;
            }

            responseMessage.Blocks[^1] = block;
            return responseMessage;
        }

        public static InteractionMessage GetMessageWithVotesClosed(IList<IMessageBlock> blocks,
            List<IList<string>> setOfGroups,
            IDictionary<string, Vote> results,
            string username)
        {
            var sb = new StringBuilder();

            foreach (var userGroup in setOfGroups)
            {
                sb.Append("Group: ");
                sb.Append(Environment.NewLine);
                var grouped = results.Where(r => userGroup.Contains(r.Key))
                    .OrderBy(v => v.Value.Value)
                    .GroupBy(vote => vote.Value.Value);
                foreach (var v in grouped)
                {
                    sb.Append(v.Key + ": ");
                    foreach (var g in v)
                    {
                        sb.Append("@" + g.Value.Username + ", ");
                    }

                    sb.Remove(sb.Length - 2, 1);
                    sb.Append(Environment.NewLine);
                }
            }

            var message = new InteractionMessage(replaceOriginal: true);
            message.Blocks = blocks;
            message.Blocks[0] = new Section()
            {
                Text = new MarkdownText(((Section) blocks[0]).Text.Text)
            };
            message.Blocks[^3] = new Section()
            {
                Text = new MarkdownText("Vote closed by @" + username)
            };
            message.Blocks[^1] = new Section()
            {
                Text = new MarkdownText(string.IsNullOrEmpty(sb.ToString()) ? "No one voted." : sb.ToString())
            };
            return message;
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