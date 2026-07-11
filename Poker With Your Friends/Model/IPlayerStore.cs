using System;

namespace Poker_With_Your_Friends.Model;

public interface IPlayerStore
{
    Player? CurrentPlayer { get; set; }
    Table? CurrentTable { get; set; }
}
