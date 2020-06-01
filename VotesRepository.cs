using System.Collections.Generic;

namespace PlanningPoker
{
    
    public interface IVotesRepository
    {
        bool AddVote(string messageId, string username, string value);

        IDictionary<string, string> GetVotes(string messageId);
    }
    
    public class VotesRepository : IVotesRepository
    {
        private IDictionary<string, IDictionary<string, string>> votesStore;

        public VotesRepository()
        {
            this.votesStore = new Dictionary<string, IDictionary<string, string>>();
        }

        public bool AddVote(string messageId, string username, string value)
        {
            var vote = new Vote()
            {
                Username = username,
                Value = value
            };
            if (votesStore.ContainsKey(messageId))
            {
                var dic = votesStore[messageId];
                if (dic.ContainsKey(vote.Username))
                {
                    dic[vote.Username] = value;
                    return false;
                }
                dic[vote.Username] = value;
                return true;
            }
            else
            {
                votesStore[messageId] = new Dictionary<string, string>
                {
                    {
                        vote.Username, vote.Value
                    }
                };
                return true;
            }
        }

        public IDictionary<string, string> GetVotes(string messageId)
        {
            return votesStore.ContainsKey(messageId) ? votesStore[messageId] : new Dictionary<string, string>();
        }
    }

    public class Vote
    {
        public string Username { get; set; }
        public string Value { get; set; }
    }
}