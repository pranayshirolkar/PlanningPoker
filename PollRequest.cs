using System.Collections.Generic;

namespace PlanningPoker
{
    // Body of POST /crmhelper/poll (called by the STCRM release workflow).
    public class PollRequest
    {
        // Optional: single-workspace deployments omit it and the app uses its sole configured token.
        public string TeamId { get; set; }
        public string Channel { get; set; }
        public string ThreadTs { get; set; }
        public string Question { get; set; }
        public List<string> UserIds { get; set; }
    }
}
