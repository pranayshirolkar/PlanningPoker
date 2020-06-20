namespace PlanningPoker
{
    public interface ISlackApiFactory
    {
        ISlackApi CreateForTeamId(string teamId);
    }
    public class SlackApiFactory : ISlackApiFactory
    {
        private readonly ITokenReader _tokenReader;

        public ISlackApi CreateForTeamId(string teamId)
        {
            var token = _tokenReader.GetToken(teamId);
            return new SlackApi(token);
        }

        public SlackApiFactory(ITokenReader tokenReader)
        {
            _tokenReader = tokenReader;
        }
    }
}