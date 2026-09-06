using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PlanningPoker
{
    public interface ITokenReader
    {
        string GetToken(string teamId);
    }

    public class TokenReader : ITokenReader
    {
        private const char Separator = ':';

        public string GetToken(string teamId)
        {
            return ResolveToken(File.ReadAllLines(".planningpokerconfig"), teamId);
        }

        public static string ResolveToken(IReadOnlyList<string> lines, string teamId)
        {
            // The machine-to-machine CreatePoll endpoint doesn't carry a Slack team_id (unlike
            // slash/interaction payloads), so with no team_id default to the sole configured token.
            var line = string.IsNullOrEmpty(teamId)
                ? lines.Single()
                : lines.Single(l => l.Split(Separator)[0].Equals(teamId));
            return line.Split(Separator, 2)[1];
        }
    }
}