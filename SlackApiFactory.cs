namespace PlanningPoker
{
    public interface ISlackApiFactory
    {
        ISlackApi CreateForTeamId(string teamId);
    }
    public class SlackApiFactory : ISlackApiFactory
    {
        private readonly ITokenReader tokenReader;

        public ISlackApi CreateForTeamId(string teamId)
        {
            var token = tokenReader.GetToken(teamId);
            return new SlackApi(token);
        }

        public SlackApiFactory(ITokenReader tokenReader)
        {
            this.tokenReader = tokenReader;
        }
    }
}