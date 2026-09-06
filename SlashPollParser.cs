using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlanningPoker
{
    // Parses `/poll "Question?" @user1 @user2 @usergroup` text. Slack sends mentions encoded as
    // <@U123|handle> and groups as <!subteam^S123|handle>.
    public static class SlashPollParser
    {
        private static readonly Regex QuestionRegex = new("\"([^\"]*)\"");
        private static readonly Regex UserRegex = new(@"<@([UW][A-Z0-9]+)(?:\|[^>]*)?>");
        private static readonly Regex GroupRegex = new(@"<!subteam\^([A-Z0-9]+)(?:\|[^>]*)?>");

        public static (string question, List<string> userIds, List<string> groupIds) Parse(string text)
        {
            text ??= string.Empty;

            var questionMatch = QuestionRegex.Match(text);
            var question = questionMatch.Success ? questionMatch.Groups[1].Value.Trim() : null;

            var userIds = UserRegex.Matches(text).Select(m => m.Groups[1].Value).Distinct().ToList();
            var groupIds = GroupRegex.Matches(text).Select(m => m.Groups[1].Value).Distinct().ToList();

            return (question, userIds, groupIds);
        }
    }
}
