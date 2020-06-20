using System.Collections.Generic;

namespace PlanningPoker
{
    public class Vote
    {
        public string Username { get; set; }
        public string Value { get; set; }
    }

    public class PokerHand
    {
        public IDictionary<string, Vote> Votes { get; set; }
        public IList<UserGroup> UserGroups { get; set; }
    }

    public class UserGroupWithUsers
    {
        public string UserGroupHandle { get; set; }
        public IList<string> UserIds { get; set; }
    }

    public class UserGroup
    {
        public string UserGroupId { get; set; }
        public string UserGroupHandle { get; set; }
    }

    public class UserAndChannel
    {
        public string UserId { get; set; }
        public string ChannelId { get; set; }

        public override int GetHashCode()
        {
            return string.Concat(UserId, ChannelId).GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            var userAndChannel = obj as UserAndChannel;
            return UserId.Equals(userAndChannel.UserId)
                   && ChannelId.Equals(userAndChannel.ChannelId);
        }
    }
}