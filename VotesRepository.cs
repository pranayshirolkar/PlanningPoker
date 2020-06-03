using System.Collections.Generic;

namespace PlanningPoker
{
    public interface IPokerHandRepository
    {
        void AddPokerHand(string messageId, IList<string> userGroups);

        bool AddVote(string messageId, string userID, string username, string value);

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
        public IList<string> UserGroups { get; set; }
    }

    public class PokerHandRepository : IPokerHandRepository
    {
        private IDictionary<string, PokerHand> pokerHandsStore;

        public PokerHandRepository()
        {
            this.pokerHandsStore = new Dictionary<string, PokerHand>();
        }

        public void AddPokerHand(string messageId, IList<string> userGroups)
        {
            pokerHandsStore[messageId] = new PokerHand()
            {
                Votes = new Dictionary<string, Vote>(),
                UserGroups = userGroups
            };
        }

        public bool AddVote(string messageId, string userID, string username, string value)
        {
            var pokerHand = pokerHandsStore[messageId];
            if (pokerHand.Votes.ContainsKey(userID))
            {
                pokerHand.Votes[userID] = new Vote()
                {
                    Username = username,
                    Value = value
                };
                return false;
            }

            pokerHand.Votes[userID] = new Vote()
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