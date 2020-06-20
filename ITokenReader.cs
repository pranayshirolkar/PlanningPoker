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
            var line = File.ReadAllLines(".planningpokerconfig");
            return line
                .Where(l => l.Split(Separator)[0].Equals(teamId))
                .Select(l => l.Split(Separator)[1])
                .Single();
        }
    }
}