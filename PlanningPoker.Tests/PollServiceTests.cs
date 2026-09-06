using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlanningPoker;
using Slack.NetStandard;
using Slack.NetStandard.Messages.Blocks;
using Slack.NetStandard.Messages.Elements;
using Slack.NetStandard.WebApi.Chat;
using Xunit;

namespace PlanningPoker.Tests
{
    public class PollServiceTests
    {
        [Theory]
        [InlineData("pollConfirm|U1,U2", true)]
        [InlineData("closeVote", false)]
        [InlineData("5", false)]
        public void CanHandle_matches_only_poll_actions(string value, bool expected)
        {
            var service = new PollService(new FakeSlackApiFactory(new FakeSlackApi()), new PollStore());

            Assert.Equal(expected, service.CanHandle(value));
        }

        [Fact]
        public async Task CreateConfirmationPoll_posts_threaded_message_with_roster_and_seeds_store()
        {
            var api = new FakeSlackApi();
            var store = new PollStore();
            var service = new PollService(new FakeSlackApiFactory(api), store);

            var (ok, ts, channel, error) = await service.CreateConfirmationPollAsync("T1", "C1",
                "1720531234.567890", "Validated?", new List<string> { "U1", "U2" });

            Assert.True(ok);
            Assert.Null(error);

            // The caller needs the poll's identity back: ts is the Slack message id and the store
            // key, and is what ThreadTs expects to reply under the poll later.
            Assert.Equal("111.222", ts);
            Assert.Equal("C0RESOLVED", channel);

            // Threaded into the release message.
            Assert.Equal("1720531234.567890", api.LastRequest.ThreadId.ToString());

            // Roster is embedded in the confirm button.
            var confirmButton = (Button) ((Actions) api.LastRequest.Blocks[2]).Elements.Single();
            Assert.Equal("pollConfirm|U1,U2", confirmButton.Value);

            // Store seeded under the returned ts, so a roster member's click counts immediately.
            var counted = await store.ConfirmAsync(api.LastResponseTs, new List<string> { "U1", "U2" },
                new string[0], "U1", _ => Task.CompletedTask);
            Assert.True(counted);
        }

        [Fact]
        public async Task CreateConfirmationPoll_without_thread_posts_top_level()
        {
            var api = new FakeSlackApi();
            var service = new PollService(new FakeSlackApiFactory(api), new PollStore());

            await service.CreateConfirmationPollAsync("T1", "C1", threadTs: null, "Q?",
                new List<string> { "U1" });

            Assert.Null(api.LastRequest.ThreadId);
        }

        [Fact]
        public async Task CreateConfirmationPoll_surfaces_slack_error()
        {
            var api = new FakeSlackApi { Ok = false, Error = "channel_not_found" };
            var service = new PollService(new FakeSlackApiFactory(api), new PollStore());

            var (ok, ts, channel, error) = await service.CreateConfirmationPollAsync("T1", "C1", null, "Q?",
                new List<string> { "U1" });

            Assert.False(ok);
            Assert.Equal("channel_not_found", error);

            // Nothing was posted, so there is no poll to identify.
            Assert.Null(ts);
            Assert.Null(channel);
        }

        private sealed class FakeSlackApiFactory : ISlackApiFactory
        {
            private readonly ISlackApi api;
            public FakeSlackApiFactory(ISlackApi api) => this.api = api;
            public ISlackApi CreateForTeamId(string teamId) => api;
        }

        private sealed class FakeSlackApi : ISlackApi
        {
            public bool Ok { get; set; } = true;
            public string Error { get; set; }
            public PostMessageRequest LastRequest { get; private set; }
            public string LastResponseTs { get; private set; }

            public Task<string> GetUserGroupHandleByUserGroupIdAsync(string userGroupId) =>
                Task.FromResult("handle");

            public Task<PostMessageResponse> SendMessageAsync(PostMessageRequest postMessageRequest)
            {
                LastRequest = postMessageRequest;
                LastResponseTs = "111.222";
                return Task.FromResult(new PostMessageResponse
                {
                    OK = Ok,
                    Error = Error,
                    Timestamp = new Timestamp(111, "222"),
                    // Slack resolves the channel; it need not match what the caller passed in.
                    Channel = "C0RESOLVED"
                });
            }

            public Task<string[]> GetUserIdsByUserGroupIdAsync(string userGroupId) =>
                Task.FromResult(new[] { "U100", "U200" });
        }
    }
}
