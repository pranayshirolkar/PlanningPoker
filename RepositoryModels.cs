using System;
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
        public string[] UserIds { get; set; }
    }

    public class UserAndChannel
    {
        public string UserId { get; set; }
        public string ChannelId { get; set; }

        protected bool Equals(UserAndChannel other)
        {
            return UserId == other.UserId && ChannelId == other.ChannelId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((UserAndChannel) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(UserId, ChannelId);
        }
    }
}