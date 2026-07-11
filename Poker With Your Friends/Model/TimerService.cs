using System;
using System.Collections.Concurrent;

namespace Poker_With_Your_Friends.Model
{
    public class TimerService
    {
        private readonly ConcurrentDictionary<Table, TableTimer> timers = new();

        public TableTimer GetOrCreateTimer(Table table)
        {
            return timers.GetOrAdd(table, t => new TableTimer(t));
        }

        public void StartTimer(Table table, int seconds)
        {
            var timer = GetOrCreateTimer(table);
            timer.StartTimer(seconds);
        }

        public void StopTimer(Table table)
        {
            if (timers.TryGetValue(table, out TableTimer? timer))
            {
                timer.StopTimer();
            }
        }

        public void Clear()
        {
            foreach (TableTimer timer in timers.Values)
            {
                timer.StopTimer();
            }

            timers.Clear();
        }
    }
}
