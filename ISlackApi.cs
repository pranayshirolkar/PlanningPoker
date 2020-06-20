using System.Linq;
using System.Threading.Tasks;
using Slack.NetStandard;
using Slack.NetStandard.WebApi.Chat;

namespace PlanningPoker
{
    public interface ISlackApi
    {
        Task<string> GetUserGroupHandleByUserGroupIdAsync(string userGroupId);
        Task<string> SendMessageAsync(PostMessageRequest postMessageRequest);
        Task<string[]> GetUserIdsByUserGroupIdAsync(string userGroupId);
    }

    public class SlackApi : ISlackApi
    {
        public async Task<string> GetUserGroupHandleByUserGroupIdAsync(string userGroupId)
        {
            var slackClient = GetSlackClient();
            var userGroups = await slackClient.Usergroups.List();
            return userGroups.Usergroups.Single(ug => ug.ID.Equals(userGroupId)).Handle;
        }

        public async Task<string> SendMessageAsync(PostMessageRequest postMessageRequest)
        {
            var slackClient = GetSlackClient();
            var response = await slackClient.Chat.Post(postMessageRequest);
            return response.Timestamp.Identifier;
        }

        public async Task<string[]> GetUserIdsByUserGroupIdAsync(string userGroupId)
        {
            var slackClient = GetSlackClient();
            var response = await slackClient.Usergroups.Users.List(userGroupId);
            return response.Users;
        }

        private static SlackWebApiClient GetSlackClient()
        {
            return new SlackWebApiClient("");
        }
    }
}