namespace PlanningPoker
{
    public class Constants
    {
        public const string CloseVote = "closeVote";
        public const string NoVotesYet = "No votes yet ...";

        // Confirmation-poll action value. The roster (expected user IDs) is appended after the
        // separator so the button carries it back — e.g. "pollConfirm|U1,U2,U3".
        public const string PollConfirmAction = "pollConfirm";
        public const char PollValueSeparator = '|';
    }
}
