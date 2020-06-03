using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Slack.NetStandard;
using Slack.NetStandard.Interaction;
using Slack.NetStandard.WebApi.Chat;

namespace PlanningPoker.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PlanningPokerController : ControllerBase
    {
        private readonly IPokerHandRepository _pokerHandRepository;
        private readonly ISlackApiClient slackClient = new SlackWebApiClient("");

        public PlanningPokerController(IPokerHandRepository pokerHandRepository)
        {
            this._pokerHandRepository = pokerHandRepository;
        }

        [Route("[action]")]
        [HttpPost]
        public async Task<IActionResult> PokerInteract([FromForm] string payload)
        {
            var p = (BlockActionsPayload) JsonConvert.DeserializeObject<InteractionPayload>(payload);

            if (p.Actions.Single().Value == Constants.CloseVote)
            {
                var pokerHand = _pokerHandRepository.GetPokerHand(p.Message.Timestamp.Identifier);
                var setOfGroups = new List<IList<string>>();
                foreach (var g in pokerHand.UserGroups)
                {
                    var group = await slackClient.Usergroups.Users.List(g);
                    var newGroupList = group.Users;
                    setOfGroups.Add(newGroupList);
                }

                var message =
                    MessageHelpers.GetMessageWithVotesClosed(p.Message.Blocks, setOfGroups, pokerHand.Votes,
                        p.User.Username);
                await message.Send(p.ResponseUrl);
            }
            else
            {
                var wasVoteAdded = _pokerHandRepository.AddVote(p.Message.Timestamp.Identifier, p.User.ID,
                    p.User.Username,
                    p.Actions.Single().Value);
                if (wasVoteAdded)
                {
                    var responseMessage = MessageHelpers.GetMessageWithNewVoteAdded(p.Message.Blocks, p.User.Username);
                    await responseMessage.Send(p.ResponseUrl);
                }
            }

            return Ok("interaction finished");
        }

        [Route("[action]")]
        [HttpPost]
        public async Task<IActionResult> Poker()
        {
            var dataset = new string[Request.Form.Count];
            var i = 0;
            foreach (var (key, value) in Request.Form)
            {
                dataset[i] = key + "=" + value;
                i++;
            }

            var payload = string.Join('&', dataset);
            var slashCommand = new SlashCommand(payload);

            var arguments = slashCommand.Text.Split(" ");
            if (arguments.Length == 1 && string.IsNullOrEmpty(arguments[0]))
            {
                var m = MessageHelpers.CreateEphemeralMessage(
                    "Please add what to deal `/poker [what]`. Optionally, to get grouped results, add groups: `/poker [@group1] [@group2] [what]`");
                await m.Send(slashCommand.ResponseUrl);
            }
            else
            {
                var userGroupIDs = new List<string>();
                var x = 0;
                while (x < arguments.Length)
                {
                    if (Regex.Match(arguments[x], @"<@U.*>").Success)
                    {
                        var m = MessageHelpers.CreateEphemeralMessage(
                            "Looks like a `@user` was tagged instead of a `@userGroup`. It will be ignored.");
                        await m.Send(slashCommand.ResponseUrl);
                    }

                    if (Regex.Match(arguments[x], @"<!subteam\^.*>").Success)
                    {
                        var userGroupId = arguments[x].Substring(10).Split('|')[0];
                        userGroupIDs.Add(userGroupId);
                    }
                    else
                    {
                        break;
                    }

                    x++;
                }

                var whatToDeal = string.Join(' ', arguments.Skip(x));

                if (string.IsNullOrEmpty(whatToDeal))
                {
                    var m = MessageHelpers.CreateEphemeralMessage(
                        "Please add what to deal `/poker [@group1] [@group2] [what]`");
                    await m.Send(slashCommand.ResponseUrl);
                }
                else
                {
                    var message = MessageHelpers.CreateDealtMessage(slashCommand.Username, whatToDeal);
                    var request = new PostMessageRequest
                    {
                        Channel = slashCommand.ChannelId
                    };
                    request.Blocks = message.Blocks;
                    var response = await slackClient.Chat.Post(request);
                    _pokerHandRepository.AddPokerHand(response.Timestamp.Identifier, userGroupIDs);
                }
            }

            return Ok("Just a moment...");
        }
    }
}