using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace PlanningPoker.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PlanningPokerController : ControllerBase
    {
        private readonly IPokerHandService pokerHandService;

        public PlanningPokerController(IPokerHandService pokerHandService)
        {
            this.pokerHandService = pokerHandService;
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

            await pokerHandService.HandleSlashCommandAsync(payload);

            return Ok();
        }
    }
}