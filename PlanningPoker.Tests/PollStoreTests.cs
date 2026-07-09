using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlanningPoker;
using Xunit;

namespace PlanningPoker.Tests
{
    public class PollStoreTests
    {
        private static readonly List<string> Roster = new() { "U1", "U2", "U3" };
        private static readonly string[] NoReconstruct = new string[0];

        [Fact]
        public void ApplyConfirm_counts_a_roster_member()
        {
            var store = new PollStore();
            store.Seed("ts1", Roster);

            var confirmed = store.ApplyConfirm("ts1", Roster, NoReconstruct, "U2", out var inRoster);

            Assert.True(inRoster);
            Assert.Equal(new[] { "U2" }, confirmed);
        }

        [Fact]
        public void ApplyConfirm_ignores_a_non_roster_clicker()
        {
            var store = new PollStore();
            store.Seed("ts1", Roster);

            var confirmed = store.ApplyConfirm("ts1", Roster, NoReconstruct, "U999", out var inRoster);

            Assert.False(inRoster);
            Assert.Empty(confirmed);
        }

        [Fact]
        public void ApplyConfirm_is_idempotent_for_repeat_clicks()
        {
            var store = new PollStore();
            store.Seed("ts1", Roster);

            store.ApplyConfirm("ts1", Roster, NoReconstruct, "U1", out _);
            var confirmed = store.ApplyConfirm("ts1", Roster, NoReconstruct, "U1", out _);

            Assert.Equal(new[] { "U1" }, confirmed);
        }

        [Fact]
        public async Task ApplyConfirm_does_not_drop_simultaneous_confirmations()
        {
            // The bug the lock prevents: read-modify-write races dropping a vote.
            var store = new PollStore();
            var bigRoster = Enumerable.Range(0, 200).Select(i => "U" + i).ToList();
            store.Seed("ts1", bigRoster);

            await Task.WhenAll(bigRoster.Select(id =>
                Task.Run(() => store.ApplyConfirm("ts1", bigRoster, NoReconstruct, id, out _))));

            var final = store.ApplyConfirm("ts1", bigRoster, NoReconstruct, "U0", out _);
            Assert.Equal(200, final.Count);
        }

        [Fact]
        public void ApplyConfirm_reconstructs_on_cold_start_miss()
        {
            // No Seed — simulates a recycled instance. Prior confirmations come from the message.
            var store = new PollStore();

            var confirmed = store.ApplyConfirm("ts1", Roster, new[] { "U1" }, "U2", out var inRoster);

            Assert.True(inRoster);
            Assert.Equal(new HashSet<string> { "U1", "U2" }, new HashSet<string>(confirmed));
        }

        [Fact]
        public void ApplyClose_returns_snapshot_and_forgets_poll()
        {
            var store = new PollStore();
            store.Seed("ts1", Roster);
            store.ApplyConfirm("ts1", Roster, NoReconstruct, "U1", out _);

            var snapshot = store.ApplyClose("ts1", Roster, NoReconstruct);
            Assert.Equal(new[] { "U1" }, snapshot);

            // After close, a fresh confirm reconstructs from an empty set (poll forgotten).
            var afterClose = store.ApplyConfirm("ts1", Roster, NoReconstruct, "U2", out _);
            Assert.Equal(new[] { "U2" }, afterClose);
        }
    }
}
