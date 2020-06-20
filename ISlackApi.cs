using System.Linq;
using System.Threading.Tasks;
using Slack.NetStandard;
using Slack.NetStandard.WebApi.Chat;

namespace PlanningPoker
{
    public interface ISlackApi
    {
        Task<string> GetUserGroupHandleByUserGroupIdAsync(string userGroupId);
        Task<PostMessageResponse> SendMessageAsync(PostMessageRequest postMessageRequest);
        Task<string[]> GetUserIdsByUserGroupIdAsync(string userGroupId);
    }

    public class SlackApi : ISlackApi
    {
        private readonly string token;
        private SlackWebApiClient SlackClient => new SlackWebApiClient(token);

        public SlackApi(string token)
        {
            this.token = token;
        }

        public async Task<string> GetUserGroupHandleByUserGroupIdAsync(string userGroupId)
        {
            var userGroups = await SlackClient.Usergroups.List();
            return userGroups.Usergroups.Single(ug => ug.ID.Equals(userGroupId)).Handle;
        }

        public async Task<PostMessageResponse> SendMessageAsync(PostMessageRequest postMessageRequest)
        {
            return await SlackClient.Chat.Post(postMessageRequest);
        }

        public async Task<string[]> GetUserIdsByUserGroupIdAsync(string userGroupId)
        {
            var response = await SlackClient.Usergroups.Users.List(userGroupId);
            return response.Users;
        }
    }
}