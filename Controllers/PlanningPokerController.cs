using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Slack.NetStandard.Interaction;

namespace PlanningPoker.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PlanningPokerController : ControllerBase
    {
        private readonly IVotesRepository votesRepository;

        public PlanningPokerController(IVotesRepository votesRepository)
        {
            this.votesRepository = votesRepository;
        }
        
        [Route("[action]")]
        [HttpPost]
        public async Task<IActionResult> PokerInteract([FromForm] string payload)
        {
            var p = (BlockActionsPayload) JsonConvert.DeserializeObject<InteractionPayload>(payload);

            if (p.Actions.Single().Value == Constants.CloseVote)
            {
                IDictionary<string, string> results = votesRepository.GetVotes(p.Message.Timestamp.Identifier);
                var message = MessageHelpers.GetMessageWithVotesClosed(p.Message.Blocks, results, p.User.Username);
                await message.Send(p.ResponseUrl);
            }
            else
            {
                var wasVoteAdded = votesRepository.AddVote(p.Message.Timestamp.Identifier, p.User.Username,
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
                var m = MessageHelpers.CreateEphemeralMessage("Please add what to deal `/poker [what] [who]`. Adding who is optional.");
                await m.Send(slashCommand.ResponseUrl);
            }
            else
            {
                var message = MessageHelpers.CreateDealtMessage(slashCommand.Username, slashCommand.Text);
                await message.Send(slashCommand.ResponseUrl);   
            }

            return Ok();
        }
    }
}