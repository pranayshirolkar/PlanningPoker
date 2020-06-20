using System.Collections.Generic;

namespace PlanningPoker
{
    public interface IPokerHandRepository
    {
        void AddPokerHand(string messageId, IList<UserGroup> userGroups);

        void AddVote(string messageId, string userId, string username, string value);

        PokerHand GetPokerHand(string messageId);

        void DeleteHand(string messageId);

        void RememberUserGroups(UserAndChannel userAndChannel, IList<UserGroup> userGroups);
        
        bool TryRetrieveSameUserGroups(UserAndChannel userAndChannel, out IList<UserGroup> userGroups);
    }

    public class PokerHandRepository : IPokerHandRepository
    {
        private readonly IDictionary<string, PokerHand> pokerHandsStore;

        private readonly IDictionary<UserAndChannel, IList<UserGroup>> rememberedUserGroups;

        public PokerHandRepository()
        {
            pokerHandsStore = new Dictionary<string, PokerHand>();
            rememberedUserGroups = new Dictionary<UserAndChannel, IList<UserGroup>>();
        }

        public void AddPokerHand(string messageId, IList<UserGroup> userGroups)
        {
            pokerHandsStore[messageId] = new PokerHand()
            {
                Votes = new Dictionary<string, Vote>(),
                UserGroups = userGroups
            };
        }

        public void AddVote(string messageId, string userId, string username, string value)
        {
            var pokerHand = pokerHandsStore[messageId];
            pokerHand.Votes[userId] = new Vote()
            {
                Username = username,
                Value = value
            };
        }

        public PokerHand GetPokerHand(string messageId)
        {
            return pokerHandsStore[messageId];
        }

        public void DeleteHand(string messageId)
        {
            pokerHandsStore.Remove(messageId);
        }

        public void RememberUserGroups(UserAndChannel userAndChannel, IList<UserGroup> userGroups)
        {
            rememberedUserGroups[userAndChannel] = userGroups;
        }

        public bool TryRetrieveSameUserGroups(UserAndChannel userAndChannel, out IList<UserGroup> userGroups)
        {
            return rememberedUserGroups.TryGetValue(userAndChannel, out userGroups);
        }
    }
}