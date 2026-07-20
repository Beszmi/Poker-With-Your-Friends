using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.Model;

public enum BlindEnum
{
    SmallBlind,
    BigBlind,
    NotBlind
}

[XmlType("Player")]
public partial class Player : ObservableObject
{
    [XmlAttribute("Name")]
    [ObservableProperty]
    public partial String Name { get; set; }

    private int chips = 0;
    [XmlAttribute("Chips")]
    public int Chips
    {
        get => chips;
        set => SetProperty(ref chips, Math.Max(0, value));
    }

    [XmlArray("Cards")]
    [XmlArrayItem("Card")]
    [ObservableProperty]
    public partial ObservableCollection<Card> Cards { get; set; } = new ObservableCollection<Card>();

    public Player() { }

    public Player(String name)
    {
        Name = name;
        chips = 100;
    }

    public Player(String name, int chips)
    {
        Name = name;
        Chips = chips;
    }

    public Player(Player p)
    {
        Name = p.Name;
        this.chips = p.chips;
    }

    public void AddCard(Card card)
    {
        Cards.Add(card);
    }

    public void ClearCards()
    {
        Cards.Clear();
    }

    [XmlIgnore]
    [ObservableProperty]
    public partial bool IsAtTable { get; set; } = false;

    [XmlAttribute("hasFolded")]
    [ObservableProperty]
    public partial bool HasFolded { get; set; } = false;

    [XmlAttribute("PotBet")]
    [ObservableProperty]
    public partial int PotBet { get; set; } = 0;

    [XmlAttribute("RoundBet")]
    [ObservableProperty]
    public partial int RoundBet { get; set; } = 0;

    [XmlAttribute("IsCurrentlyActivePlayer")]
    [ObservableProperty]
    public partial bool IsCurrentlyActivePlayer { get; set; }

    [XmlAttribute("IsAllIn")]
    [ObservableProperty]
    public partial bool IsAllIn { get; set; } = false;

    [XmlAttribute("Wonlast")]
    [ObservableProperty]
    public partial bool WonLast { get; set; } = false;

    [XmlIgnore]
    [ObservableProperty]
    public partial Hand? Hand { get; set; }

    [XmlAttribute("Blind")]
    [ObservableProperty]
    public partial BlindEnum Blind { get; set; } = BlindEnum.NotBlind;

    [XmlAttribute("CardsRevealed")]
    [ObservableProperty]
    public partial bool CardsRevealed { get; set; } = false;

    [XmlAttribute("ProfilePicture")]
    [ObservableProperty]
    public partial String ProfilePictureDir { get; set; } = "/Assets/pfp/Emptypfp.jpg";

    // #AARRGGBB
    public string BgColor => WonLast ? "#99FFFF00" : "#9900FF00";

    public string HandName => Hand?.ToString() ?? string.Empty;

    partial void OnWonLastChanged(bool value)
    {
        OnPropertyChanged(nameof(BgColor));
    }

    partial void OnHandChanged(Hand? value)
    {
        OnPropertyChanged(nameof(HandName));
    }

    public void Fold() { HasFolded = true; }

    public void Spend(int amount)
    {
        if (amount > chips)
        {
            throw new ArgumentOutOfRangeException("Not enough chips");
        }
        SetProperty(ref chips, chips - amount, nameof(Chips));
        PotBet += amount;
        RoundBet += amount;
    }

    public void Win(int amount)
    {
        Chips += amount;
        WonLast = true;
    }

    public void AddProfilePicture(String Dir)
    {
        ProfilePictureDir = Dir;
    }

    public void UpdateProperties(Player NewPlayer)
    {
        Chips = NewPlayer.Chips;
        // IsAtTable Should be handled by joining and leaving code
        HasFolded = NewPlayer.HasFolded;
        RoundBet = NewPlayer.RoundBet;
        PotBet = NewPlayer.PotBet;
        IsCurrentlyActivePlayer = NewPlayer.IsCurrentlyActivePlayer;
        IsAllIn = NewPlayer.IsAllIn;
        WonLast = NewPlayer.WonLast;
        Blind = NewPlayer.Blind;
        CardsRevealed = NewPlayer.CardsRevealed;
        ProfilePictureDir = Game.GetProfilePicturePath(Name);

        Cards.Clear();
        foreach (var card in NewPlayer.Cards)
        {
            Cards.Add(card);
        }
    }
}
