using System.Collections.Generic;

namespace PlanningPoker
{
    // Body of POST /PlanningPoker/CreatePoll (called by the STCRM release workflow).
    public class PollRequest
    {
        public string TeamId { get; set; }
        public string Channel { get; set; }
        public string ThreadTs { get; set; }
        public string Question { get; set; }
        public List<string> UserIds { get; set; }
    }
}
