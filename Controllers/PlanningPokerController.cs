using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace PlanningPoker.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PlanningPokerController : ControllerBase
    {
        private readonly IPokerHandService pokerHandService;
        private readonly IPollService pollService;
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment environment;

        public PlanningPokerController(IPokerHandService pokerHandService, IPollService pollService,
            IConfiguration configuration, IWebHostEnvironment environment)
        {
            this.pokerHandService = pokerHandService;
            this.pollService = pollService;
            this.configuration = configuration;
            this.environment = environment;
        }

        [Route("[action]")]
        [HttpPost]
        public async Task<IActionResult> PokerInteract([FromForm] string payload)
        {
            await pokerHandService.HandleInteractionAsync(payload);
            return Ok("interaction finished");
        }

        [Route("[action]")]
        [HttpGet]
        public IActionResult Hello()
        {
            return Ok("Hello from Planning Poker App!");
        }

        // Neutral warm-up/health route for machine-to-machine callers (the STCRM release workflow)
        // so they don't reference the app name.
        [HttpGet("/crmhelper/health")]
        public IActionResult Health()
        {
            return Ok("ok");
        }

        [Route("[action]")]
        [HttpPost]
        public async Task<IActionResult> Poker()
        {
            await pokerHandService.HandleSlashCommandAsync(FormPayload());
            return Ok();
        }

        // Slash command: /poll "Question?" @user1 @user2 [@usergroup]
        [Route("[action]")]
        [HttpPost]
        public async Task<IActionResult> Poll()
        {
            await pollService.HandleSlashCommandAsync(FormPayload());
            return Ok();
        }

        // Machine-to-machine entry point (STCRM release workflow). Neutral route (no controller
        // prefix) so callers don't reference the app name. Authenticated by an HMAC-SHA256
        // signature over the raw body, not by Slack's request signing.
        [HttpPost("/crmhelper/poll")]
        public async Task<IActionResult> CreatePoll()
        {
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync();

            var secret = configuration["PlanningPoker:PollSecret"];
            var signature = Request.Headers[SignatureVerifier.HeaderName].ToString();
            var devBypass = environment.IsDevelopment() && string.IsNullOrEmpty(secret);
            if (!devBypass && !SignatureVerifier.VerifyHmacSha256(rawBody, secret, signature))
            {
                return Unauthorized();
            }

            PollRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<PollRequest>(rawBody);
            }
            catch (JsonException)
            {
                return BadRequest(new { ok = false, error = "invalid_json" });
            }

            if (request == null || string.IsNullOrEmpty(request.Channel)
                || string.IsNullOrEmpty(request.Question) || request.UserIds == null || !request.UserIds.Any())
            {
                return BadRequest(new { ok = false, error = "channel, question and at least one userId are required" });
            }

            var (ok, ts, channel, error) = await pollService.CreateConfirmationPollAsync(request.TeamId,
                request.Channel, request.ThreadTs, request.Question, request.UserIds.Distinct().ToList());

            // ts identifies the poll: it is the Slack message id, and what ThreadTs expects if the
            // caller later wants to reply under it.
            return ok
                ? Ok(new { ok = true, ts, channel })
                : StatusCode(502, new { ok = false, error });
        }

        private string FormPayload()
        {
            var dataset = new string[Request.Form.Count];
            var i = 0;
            foreach (var (key, value) in Request.Form)
            {
                dataset[i] = key + "=" + value;
                i++;
            }

            return string.Join('&', dataset);
        }
    }
}
