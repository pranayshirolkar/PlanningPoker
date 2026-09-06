using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Slack.NetStandard.Interaction;
using Slack.NetStandard.WebApi.Chat;

namespace PlanningPoker
{
    public interface IPollService
    {
        // True if this interaction is a confirmation-poll action (so the shared interactivity
        // endpoint can route it here instead of to the planning-poker handler).
        bool CanHandle(string actionValue);

        // ts and channel identify the poll for linking to it or threading onto it later; channel is
        // Slack's resolved id, not necessarily the one passed in. Both are null when ok is false.
        Task<(bool ok, string ts, string channel, string error)> CreateConfirmationPollAsync(string teamId,
            string channel, string threadTs, string question, IReadOnlyList<string> roster);

        Task HandleSlashCommandAsync(string payload);

        Task HandleInteractionAsync(BlockActionsPayload payload);
    }

    public class PollService : IPollService
    {
        private readonly ISlackApiFactory slackApiFactory;
        private readonly IPollStore pollStore;

        public PollService(ISlackApiFactory slackApiFactory, IPollStore pollStore)
        {
            this.slackApiFactory = slackApiFactory;
            this.pollStore = pollStore;
        }

        public bool CanHandle(string actionValue)
        {
            var (action, _) = PollMessageHelpers.ParseActionValue(actionValue);
            return action == Constants.PollConfirmAction;
        }

        public async Task<(bool ok, string ts, string channel, string error)> CreateConfirmationPollAsync(
            string teamId, string channel, string threadTs, string question, IReadOnlyList<string> roster)
        {
            var request = new PostMessageRequest
            {
                Channel = channel,
                Blocks = PollMessageHelpers.BuildInitialBlocks(question, roster)
            };
            if (!string.IsNullOrEmpty(threadTs))
            {
                request.ThreadId = PollMessageHelpers.ParseTimestamp(threadTs);
            }

            var response = await slackApiFactory.CreateForTeamId(teamId).SendMessageAsync(request);
            if (!response.OK)
            {
                return (false, null, null, response.Error);
            }

            var ts = response.Timestamp.ToString();
            pollStore.Seed(ts, roster);
            return (true, ts, response.Channel, null);
        }

        public async Task HandleSlashCommandAsync(string payload)
        {
            var command = new SlashCommand(payload);
            var (question, userIds, groupIds) = SlashPollParser.Parse(command.Text);

            if (string.IsNullOrEmpty(question))
            {
                await MessageHelpers
                    .CreateEphemeralMessage("Usage: `/poll \"Your question?\" @user1 @user2 [@usergroup]`")
                    .Send(command.ResponseUrl);
                return;
            }

            var slackApi = slackApiFactory.CreateForTeamId(command.TeamId);
            var roster = new List<string>(userIds);
            foreach (var groupId in groupIds)
            {
                roster.AddRange(await slackApi.GetUserIdsByUserGroupIdAsync(groupId));
            }

            roster = roster.Distinct().ToList();
            if (!roster.Any())
            {
                await MessageHelpers
                    .CreateEphemeralMessage("Please tag at least one `@user` or `@usergroup` to poll.")
                    .Send(command.ResponseUrl);
                return;
            }

            var (ok, _, _, error) = await CreateConfirmationPollAsync(command.TeamId, command.ChannelId,
                threadTs: null, question, roster);
            if (!ok)
            {
                var message = error == "channel_not_found"
                    ? "Please invite @planningpoker if this is a private channel. `/poll` is only supported in channels and groups."
                    : "Unexpected error creating the poll, sorry!";
                await MessageHelpers.CreateEphemeralMessage(message).Send(command.ResponseUrl);
            }
        }

        public async Task HandleInteractionAsync(BlockActionsPayload payload)
        {
            var (_, roster) = PollMessageHelpers.ParseActionValue(payload.Actions.Single().Value);
            var confirmedFromBlocks = PollMessageHelpers.ParseConfirmedFromBlocks(payload.Message.Blocks);

            // Completion is derived from the confirmed set rather than stored, so a duplicate or late
            // click re-renders the finished message instead of reopening it.
            var counted = await pollStore.ConfirmAsync(payload.Message.Timestamp.ToString(), roster,
                confirmedFromBlocks, payload.User.ID, async confirmed =>
                {
                    var confirmedSet = new HashSet<string>(confirmed);
                    var message = roster.All(confirmedSet.Contains)
                        ? PollMessageHelpers.BuildCompletedUpdate(payload.Message.Blocks, roster)
                        : PollMessageHelpers.BuildActiveUpdate(payload.Message.Blocks, roster, confirmedSet);
                    await message.Send(payload.ResponseUrl);
                });

            if (!counted)
            {
                await MessageHelpers
                    .CreateEphemeralMessage("You're not in this poll's participant list, so your click wasn't counted.")
                    .Send(payload.ResponseUrl);
            }
        }
    }
}
