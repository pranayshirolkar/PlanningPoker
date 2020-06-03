using System.Collections.Generic;

namespace PlanningPoker
{
    public interface IPokerHandRepository
    {
        void AddPokerHand(string messageId, IList<UserGroup> userGroups);

        bool AddVote(string messageId, string userId, string username, string value);

        PokerHand GetPokerHand(string messageId);
    }

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

    public class PokerHandRepository : IPokerHandRepository
    {
        private IDictionary<string, PokerHand> pokerHandsStore;

        public PokerHandRepository()
        {
            this.pokerHandsStore = new Dictionary<string, PokerHand>();
        }

        public void AddPokerHand(string messageId, IList<UserGroup> userGroups)
        {
            pokerHandsStore[messageId] = new PokerHand()
            {
                Votes = new Dictionary<string, Vote>(),
                UserGroups = userGroups
            };
        }

        public bool AddVote(string messageId, string userId, string username, string value)
        {
            var pokerHand = pokerHandsStore[messageId];
            if (pokerHand.Votes.ContainsKey(userId))
            {
                pokerHand.Votes[userId] = new Vote()
                {
                    Username = username,
                    Value = value
                };
                return false;
            }

            pokerHand.Votes[userId] = new Vote()
            {
                Username = username,
                Value = value
            };
            return true;
        }

        public PokerHand GetPokerHand(string messageId)
        {
            return pokerHandsStore[messageId];
        }
    }
}