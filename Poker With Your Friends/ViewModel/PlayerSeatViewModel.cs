using Poker_With_Your_Friends.Model;

namespace Poker_With_Your_Friends.ViewModel;

public sealed class PlayerSeatViewModel
{
    public PlayerSeatViewModel(Player player, InGamePageViewModel owner, bool isLocalPlayer, bool useRevealedTemplate)
    {
        Player = player;
        Owner = owner;
        IsLocalPlayer = isLocalPlayer;
        UseRevealedTemplate = useRevealedTemplate;
    }

    public Player Player { get; }

    public InGamePageViewModel Owner { get; }

    public bool IsLocalPlayer { get; }

    public bool UseRevealedTemplate { get; }
}
