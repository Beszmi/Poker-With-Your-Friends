using System;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using CommunityToolkit.Mvvm.ComponentModel;
using Poker_With_Your_Friends;

namespace Poker_With_Your_Friends.Model;

public partial class TableTimer : ObservableObject
{
    private readonly DispatcherQueueTimer timer;
    private readonly DispatcherQueue _dispatcherQueue;

    public event Action? Expired;

    [ObservableProperty]
    public partial TimeSpan Remaining { get; set; }

    [ObservableProperty]
    public partial TimeSpan Total { get; set; } = TimeSpan.FromSeconds(60);
    public Table TimedTable { get; }

    public TableTimer(Table table)
    {
        TimedTable = table;
        _dispatcherQueue = App.MainDispatcher ?? DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("UI dispatcher is not available.");
        timer = _dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        timer.Tick += Timer_Tick;
        Remaining = Total;
    }

    public void StartTimer(int seconds)
    {
        if (!_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => StartTimer(seconds));
            return;
        }

        Total = TimeSpan.FromSeconds(seconds);
        Remaining = Total;
        timer.Start();
    }

    public void StopTimer()
    {
        if (!_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(StopTimer);
            return;
        }

        timer.Stop();
    }

    private void Timer_Tick(DispatcherQueueTimer sender, object args)
    {
        Remaining -= sender.Interval;
        if (Remaining <= TimeSpan.Zero)
        {
            Remaining = TimeSpan.Zero;
            sender.Stop();
            Expired?.Invoke();
        }
    }
}
