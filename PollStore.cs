using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace PlanningPoker
{
    // Authoritative confirmed-set state, keyed by the poll message's ts. In-memory is sufficient
    // because the app runs as a single instance (free-tier Azure App Service, no scale-out); an
    // in-process lock per message serializes the read-modify-write so simultaneous clicks can't
    // drop a confirmation. On a cold-start miss the state is reconstructed once from the message
    // (best-effort) via the confirmedFromBlocks the caller supplies.
    public interface IPollStore
    {
        void Seed(string messageTs, IEnumerable<string> roster);

        // Adds the clicker (if a roster member) and returns the confirmed snapshot. clickerInRoster
        // tells the caller whether the click counted.
        IReadOnlyCollection<string> ApplyConfirm(string messageTs, IReadOnlyList<string> roster,
            IEnumerable<string> confirmedFromBlocks, string clickerId, out bool clickerInRoster);

        // Returns the confirmed snapshot and forgets the poll.
        IReadOnlyCollection<string> ApplyClose(string messageTs, IReadOnlyList<string> roster,
            IEnumerable<string> confirmedFromBlocks);

        void Remove(string messageTs);
    }

    public class PollStore : IPollStore
    {
        private readonly ConcurrentDictionary<string, PollState> store = new();

        public void Seed(string messageTs, IEnumerable<string> roster)
        {
            store[messageTs] = new PollState(roster, new string[0]);
        }

        public IReadOnlyCollection<string> ApplyConfirm(string messageTs, IReadOnlyList<string> roster,
            IEnumerable<string> confirmedFromBlocks, string clickerId, out bool clickerInRoster)
        {
            var state = store.GetOrAdd(messageTs, _ => new PollState(roster, confirmedFromBlocks));
            lock (state.Sync)
            {
                clickerInRoster = state.Roster.Contains(clickerId);
                if (clickerInRoster)
                {
                    state.Confirmed.Add(clickerId);
                }

                return state.Confirmed.ToList();
            }
        }

        public IReadOnlyCollection<string> ApplyClose(string messageTs, IReadOnlyList<string> roster,
            IEnumerable<string> confirmedFromBlocks)
        {
            var state = store.GetOrAdd(messageTs, _ => new PollState(roster, confirmedFromBlocks));
            lock (state.Sync)
            {
                var snapshot = state.Confirmed.ToList();
                store.TryRemove(messageTs, out _);
                return snapshot;
            }
        }

        public void Remove(string messageTs) => store.TryRemove(messageTs, out _);

        private sealed class PollState
        {
            public object Sync { get; } = new();
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
