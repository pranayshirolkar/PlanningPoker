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

        // Retired in favour of closing automatically once everyone has confirmed. Still recognised
        // so a click on a poll posted before the change is swallowed with an explanation, rather
        // than falling through to the planning-poker vote path — which indexes the hand store by
        // message ts and would throw on a poll's ts.
        public const string RetiredPollCloseAction = "pollClose";
    }
}
