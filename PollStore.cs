using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlanningPoker
{
    // Authoritative confirmed-set state, keyed by the poll message's ts. In-memory is sufficient
    // because the app runs as a single instance (free-tier Azure App Service, no scale-out); a
    // per-message semaphore serialises the read-modify-write *and* the Slack render, so simultaneous
    // clicks can neither drop a confirmation nor land their updates out of order. On a cold-start
    // miss the state is reconstructed once from the message (best-effort) via the
    // confirmedFromBlocks the caller supplies.
    //
    // Entries are never evicted. Dropping one on completion would hand the next click a fresh state
    // object while an earlier click still held the old one's semaphore, losing the mutual exclusion
    // the semaphore exists for. Growth is per poll created, not per interaction. See #3.
    public interface IPollStore
    {
        void Seed(string messageTs, IEnumerable<string> roster);

        // Adds the clicker and invokes render with the resulting confirmed set, both under the
        // poll's lock. Returns false — without rendering — if the clicker isn't in the roster.
        Task<bool> ConfirmAsync(string messageTs, IReadOnlyList<string> roster,
            IEnumerable<string> confirmedFromBlocks, string clickerId,
            Func<IReadOnlyCollection<string>, Task> render);
    }

    public class PollStore : IPollStore
    {
        private readonly ConcurrentDictionary<string, PollState> store = new();

        public void Seed(string messageTs, IEnumerable<string> roster)
        {
            // GetOrAdd, not an assignment: a click can reach the interactivity endpoint before
            // chat.postMessage has returned here, and clobbering would drop what it recorded.
            store.GetOrAdd(messageTs, _ => new PollState(roster, new string[0]));
        }

        public async Task<bool> ConfirmAsync(string messageTs, IReadOnlyList<string> roster,
            IEnumerable<string> confirmedFromBlocks, string clickerId,
            Func<IReadOnlyCollection<string>, Task> render)
        {
            // GetOrAdd may run the factory more than once under contention, but every caller gets
            // back the one instance that was actually stored — so they all share a semaphore.
            var state = store.GetOrAdd(messageTs, _ => new PollState(roster, confirmedFromBlocks));

            // Roster is fixed at construction, so this read needs no lock.
            if (!state.Roster.Contains(clickerId))
            {
                return false;
            }

            await state.Gate.WaitAsync();
            try
            {
                state.Confirmed.Add(clickerId);
                await render(state.Confirmed.ToList());
            }
            finally
            {
                state.Gate.Release();
            }

            return true;
        }

        private sealed class PollState
        {
            public SemaphoreSlim Gate { get; } = new(1, 1);
            public HashSet<string> Roster { get; }
            public HashSet<string> Confirmed { get; }

            public PollState(IEnumerable<string> roster, IEnumerable<string> confirmed)
            {
                Roster = new HashSet<string>(roster);
                // A confirmed id that isn't in the roster can't happen on the warm path (we gate on
                // membership), but filter defensively for the reconstruction path.
                Confirmed = new HashSet<string>(confirmed.Where(Roster.Contains));
            }
        }
    }
}
