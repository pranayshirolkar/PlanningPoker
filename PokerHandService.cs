using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Slack.NetStandard.Interaction;
using Slack.NetStandard.WebApi.Chat;

namespace PlanningPoker
{
    public interface IPokerHandService
    {
        Task HandleSlashCommandAsync(string payload);
        Task HandleInteractionAsync(string payload);
    }

    public class PokerHandService : IPokerHandService
    {
        private readonly IPokerHandRepository pokerHandRepository;
        private readonly ISlackApiFactory slackApiFactory;

        public async Task HandleSlashCommandAsync(string payload)
        {
            var slashCommand = new SlashCommand(payload);

            var arguments = slashCommand.Text.Split(" ");
            if (arguments.Length == 1 && string.IsNullOrEmpty(arguments[0]))
            {
                var m = MessageHelpers.CreateEphemeralMessage(
                    "Please add what to deal `/poker [what]`. Optionally, to get grouped results, add groups: `/poker [@group1] [@group2] [what]`");
                await m.Send(slashCommand.ResponseUrl);
            }
            else
            {
                if (arguments[0] == "help")
                {
                    var m = MessageHelpers.CreateEphemeralMessage(
                        "To deal in a channel or group, type `/poker [what]`\n" +
                        "To get grouped results, add user groups: `/poker [@group1] [@group2] [what]`\n" +
                        "To use the same groups in that channel subsequently: `/poker --same [what]`");
                    await m.Send(slashCommand.ResponseUrl);

                    return;
                }

                var userGroups = new List<UserGroup>();
                var x = 0;
                while (x < arguments.Length)
                {
                    if (Regex.Match(arguments[x], @"<@U.*>").Success)
                    {
                        var m = MessageHelpers.CreateEphemeralMessage(
                            "Looks like a `@user` was tagged instead of a `@userGroup`. It will be ignored.");
                        await m.Send(slashCommand.ResponseUrl);
                    }

                    if (Regex.Match(arguments[x], @"<!subteam\^.*>|--same").Success)
                    {
                        if (arguments[x].Equals("--same"))
                        {
                            if (pokerHandRepository.TryRetrieveSameUserGroups(new UserAndChannel
                                {
                                    ChannelId = slashCommand.ChannelId,
                                    UserId = slashCommand.UserId
                                },
                                out var retrievedUserGroups))
                            {
                                userGroups.AddRange(retrievedUserGroups);
                            }
                            else
                            {
                                var cannotRememberMessage = MessageHelpers.CreateEphemeralMessage(
                                    "I'm sorry I can't remember `--same`.\n" +
                                    "Please use the command with groups once: `/poker [@group1] [@group2] [what]`.\n" +
                                    "Subsequently, to use the same ones next time in this channel, use `/poker --same [what]`.\n" +
                                    "");
                                await cannotRememberMessage.Send(slashCommand.ResponseUrl);
                                return;
                            }
                        }
                        else
                        {
                            var userGroupId = arguments[x].Substring(10).Split('|')[0];
                            var userGroupHandle = await slackApiFactory.CreateForTeamId(slashCommand.TeamId)
                                .GetUserGroupHandleByUserGroupIdAsync(userGroupId);
                            userGroups.Add(new UserGroup()
                            {
                                UserGroupHandle = userGroupHandle,
                                UserGroupId = userGroupId
                            });
                        }
                    }
                    else
                    {
                        break;
                    }

                    x++;
                }

                var whatToDeal = string.Join(' ', arguments.Skip(x));

                if (string.IsNullOrEmpty(whatToDeal))
                {
                    var m = MessageHelpers.CreateEphemeralMessage(
                        "Please add what to deal `/poker [@group1] [@group2] [what]`");
                    await m.Send(slashCommand.ResponseUrl);
                }
                else
                {
                    var message = MessageHelpers.CreateDealtMessage(slashCommand.Username, whatToDeal, userGroups);
                    var request = new PostMessageRequest
                    {
                        Channel = slashCommand.ChannelId,
                        Blocks = message.Blocks
                    };
                    var response = await slackApiFactory.CreateForTeamId(slashCommand.TeamId)
                        .SendMessageAsync(request);
                    if (!response.OK)
                    {
                        if (response.Error.Equals("channel_not_found"))
                        {
                            await MessageHelpers
                                .CreateEphemeralMessage(
                                    "Please invite @planningpoker if this is a private channel.\n" +
                                    "This command is only useful (and supported) in groups and channels.\n" +
                                    "_Not supported in Direct Messages._")
                                .Send(slashCommand.ResponseUrl);
                        }
                        else
                        {
                            await MessageHelpers.CreateEphemeralMessage("Unexpected error occured, sorry!")
                                .Send(slashCommand.ResponseUrl);
                        }

                        return;
                    }

                    pokerHandRepository.AddPokerHand(response.Timestamp.ToString(), userGroups);
                    if (userGroups.Any())
                    {
                        pokerHandRepository.RememberUserGroups(new UserAndChannel
                        {
                            ChannelId = slashCommand.ChannelId,
                            UserId = slashCommand.UserId
                        }, userGroups);
                    }
                }
            }
        }

        public async Task HandleInteractionAsync(string payload)
        {
            var p = (BlockActionsPayload) JsonConvert.DeserializeObject<InteractionPayload>(payload);

            if (p.Actions.Single().Value == Constants.CloseVote)
            {
                var pokerHand = pokerHandRepository.GetPokerHand(p.Message.Timestamp.ToString());
                var setOfGroups = new List<UserGroupWithUsers>();
                foreach (var g in pokerHand.UserGroups)
                {
                    var userIds = await slackApiFactory.CreateForTeamId(p.Team.ID)
                        .GetUserIdsByUserGroupIdAsync(g.UserGroupId);
                    setOfGroups.Add(new UserGroupWithUsers()
                    {
                        UserIds = userIds,
                        UserGroupHandle = g.UserGroupHandle
                    });
                }

                var message =
                    MessageHelpers.GetMessageWithVotesClosed(p.Message.Blocks, setOfGroups, pokerHand.Votes,
                        p.User.Username);
                await message.Send(p.ResponseUrl);
                pokerHandRepository.DeleteHand(p.Message.Timestamp.ToString());
            }
            else
            {
                pokerHandRepository.AddVote(p.Message.Timestamp.ToString(), p.User.ID,
                    p.User.Username,
                    p.Actions.Single().Value);
                var pokerHand = pokerHandRepository.GetPokerHand(p.Message.Timestamp.ToString());
                var responseMessage = MessageHelpers.GetMessageWithNewVoteAdded(p.Message.Blocks,
                    pokerHand.Votes.Select(i => i.Value.Username).ToList());
                await responseMessage.Send(p.ResponseUrl);
            }
        }

        public PokerHandService(IPokerHandRepository pokerHandRepository, ISlackApiFactory slackApiFactory)
        {
            this.pokerHandRepository = pokerHandRepository;
            this.slackApiFactory = slackApiFactory;
        }
    }
}