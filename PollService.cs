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

        Task<(bool ok, string error)> CreateConfirmationPollAsync(string teamId, string channel,
            string threadTs, string question, IReadOnlyList<string> roster);

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
            return action == Constants.PollConfirmAction || action == Constants.PollCloseAction;
        }

        public async Task<(bool ok, string error)> CreateConfirmationPollAsync(string teamId, string channel,
            string threadTs, string question, IReadOnlyList<string> roster)
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
                return (false, response.Error);
            }

            pollStore.Seed(response.Timestamp.ToString(), roster);
            return (true, null);
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

            var (ok, error) = await CreateConfirmationPollAsync(command.TeamId, command.ChannelId,
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
            var (action, roster) = PollMessageHelpers.ParseActionValue(payload.Actions.Single().Value);
            var messageTs = payload.Message.Timestamp.ToString();
            var confirmedFromBlocks = PollMessageHelpers.ParseConfirmedFromBlocks(payload.Message.Blocks);

            if (action == Constants.PollCloseAction)
            {
                var confirmed = pollStore.ApplyClose(messageTs, roster, confirmedFromBlocks);
                var closed = PollMessageHelpers.BuildClosedUpdate(payload.Message.Blocks, roster,
                    new HashSet<string>(confirmed), payload.User.ID);
                await closed.Send(payload.ResponseUrl);
                return;
            }

            var updated = pollStore.ApplyConfirm(messageTs, roster, confirmedFromBlocks, payload.User.ID,
                out var clickerInRoster);
            if (!clickerInRoster)
            {
                await MessageHelpers
                    .CreateEphemeralMessage("You're not in this poll's participant list, so your click wasn't counted.")
                    .Send(payload.ResponseUrl);
                return;
            }

            var confirmedSet = new HashSet<string>(updated);
            if (roster.All(confirmedSet.Contains))
            {
                pollStore.Remove(messageTs);
                var closed = PollMessageHelpers.BuildClosedUpdate(payload.Message.Blocks, roster, confirmedSet,
                    closedByUserId: null);
                await closed.Send(payload.ResponseUrl);
            }
            else
            {
                var active = PollMessageHelpers.BuildActiveUpdate(payload.Message.Blocks, roster, confirmedSet);
                await active.Send(payload.ResponseUrl);
            }
        }
    }
}
