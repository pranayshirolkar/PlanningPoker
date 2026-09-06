using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlanningPoker;
using Xunit;

namespace PlanningPoker.Tests
{
    public class PollStoreTests
    {
        private static readonly List<string> Roster = new() { "U1", "U2", "U3" };
        private static readonly string[] NoReconstruct = new string[0];

        // Captures the confirmed set the store hands to the render callback.
        private static Func<IReadOnlyCollection<string>, Task> Capture(List<string> into) =>
            confirmed =>
            {
                into.Clear();
                into.AddRange(confirmed);
                return Task.CompletedTask;
            };

        [Fact]
        public async Task ConfirmAsync_counts_a_roster_member()
        {
            var store = new PollStore();
            store.Seed("ts1", Roster);
            var rendered = new List<string>();

            var counted = await store.ConfirmAsync("ts1", Roster, NoReconstruct, "U2", Capture(rendered));

            Assert.True(counted);
            Assert.Equal(new[] { "U2" }, rendered);
        }

        [Fact]
        public async Task ConfirmAsync_ignores_a_non_roster_clicker_without_rendering()
        {
            var store = new PollStore();
            store.Seed("ts1", Roster);
            var renderCalls = 0;

            var counted = await store.ConfirmAsync("ts1", Roster, NoReconstruct, "U999", _ =>
            {
                renderCalls++;
                return Task.CompletedTask;
            });

            Assert.False(counted);
            Assert.Equal(0, renderCalls);
        }

        [Fact]
        public async Task ConfirmAsync_is_idempotent_for_repeat_clicks()
        {
            var store = new PollStore();
            store.Seed("ts1", Roster);
            var rendered = new List<string>();

            await store.ConfirmAsync("ts1", Roster, NoReconstruct, "U1", Capture(rendered));
            await store.ConfirmAsync("ts1", Roster, NoReconstruct, "U1", Capture(rendered));

            Assert.Equal(new[] { "U1" }, rendered);
        }

        [Fact]
        public async Task ConfirmAsync_does_not_drop_simultaneous_confirmations()
        {
            // The bug the lock prevents: read-modify-write races dropping a vote.
            var store = new PollStore();
            var bigRoster = Enumerable.Range(0, 200).Select(i => "U" + i).ToList();
            store.Seed("ts1", bigRoster);

            await Task.WhenAll(bigRoster.Select(id => Task.Run(() =>
                store.ConfirmAsync("ts1", bigRoster, NoReconstruct, id, _ => Task.CompletedTask))));

            var final = new List<string>();
            await store.ConfirmAsync("ts1", bigRoster, NoReconstruct, "U0", Capture(final));
            Assert.Equal(200, final.Count);
        }

        [Fact]
        public async Task ConfirmAsync_serialises_renders_for_one_poll()
        {
            // The render is the Slack send. Overlapping renders can reach Slack out of order and
            // leave the message showing a stale tally — or an active one after completion.
            var store = new PollStore();
            var bigRoster = Enumerable.Range(0, 50).Select(i => "U" + i).ToList();
            store.Seed("ts1", bigRoster);

            var inRender = 0;
            var overlaps = 0;

            await Task.WhenAll(bigRoster.Select(id => Task.Run(() =>
                store.ConfirmAsync("ts1", bigRoster, NoReconstruct, id, async _ =>
                {
                    if (Interlocked.Increment(ref inRender) > 1)
                    {
                        Interlocked.Increment(ref overlaps);
                    }

                    await Task.Delay(1);
                    Interlocked.Decrement(ref inRender);
                }))));

            Assert.Equal(0, overlaps);
        }

        [Fact]
        public async Task ConfirmAsync_reconstructs_on_cold_start_miss()
        {
            // No Seed — simulates a recycled instance. Prior confirmations come from the message.
            var store = new PollStore();
            var rendered = new List<string>();

            var counted = await store.ConfirmAsync("ts1", Roster, new[] { "U1" }, "U2", Capture(rendered));

            Assert.True(counted);
            Assert.Equal(new HashSet<string> { "U1", "U2" }, new HashSet<string>(rendered));
        }

        [Fact]
        public async Task ConfirmAsync_keeps_state_after_the_poll_completes()
        {
            // State is never evicted, so a late duplicate click still sees the full confirmed set and
            // re-derives completion instead of reopening the poll at 1/N.
            var store = new PollStore();
            store.Seed("ts1", Roster);
            foreach (var id in Roster)
            {
                await store.ConfirmAsync("ts1", Roster, NoReconstruct, id, _ => Task.CompletedTask);
            }

            var rendered = new List<string>();
            await store.ConfirmAsync("ts1", Roster, NoReconstruct, "U3", Capture(rendered));

            Assert.Equal(new HashSet<string>(Roster), new HashSet<string>(rendered));
        }
    }
}
