using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Poker_With_Your_Friends.Model;

public enum HandRank
{
    HighCard,
    Pair,
    Twopair,
    ThreeOfAKind,
    Straight,
    Flush,
    FullHouse,
    FourOfAKind,
    StraightFlush,
    RoyalFlush
}

public partial class Hand : ObservableObject, IComparable<Hand>
{
    [ObservableProperty]
    public partial HandRank Rank { get; private set; }
    [ObservableProperty]
    public partial int HighestValue { get; private set; }
    public int[] Kickers { get; private set; } = Array.Empty<int>();

    public Hand(Card[] cards)
    {
        if (cards is null || cards.Length is < 2 or > 7)
            throw new ArgumentException("Hand requires 2 to 7 cards.", nameof(cards));

        foreach (var card in cards)
        {
            if (card is null || card.Value is < 1 or > 13)
                throw new ArgumentException("Each card must have a rank between 1 (Ace) and 13 (King).", nameof(cards));
        }

        var best = cards.Length switch
        {
            < 5 => EvaluatePartialCards(cards),
            5 => EvaluateFiveCards(cards),
            _ => FindBestHand(cards)
        };

        Rank = best.Rank;
        Kickers = best.Kickers;
        HighestValue = Kickers.Length > 0 ? Kickers[0] : 0;
    }

    public int CompareTo(Hand? other)
    {
        if (other is null) return 1;

        int rankCompare = Rank.CompareTo(other.Rank);
        if (rankCompare != 0) return rankCompare;

        int length = Math.Min(Kickers.Length, other.Kickers.Length);
        for (int i = 0; i < length; i++)
        {
            int kickerCompare = Kickers[i].CompareTo(other.Kickers[i]);
            if (kickerCompare != 0) return kickerCompare;
        }

        return Kickers.Length.CompareTo(other.Kickers.Length);
    }

    private static (HandRank Rank, int[] Kickers) FindBestHand(Card[] cards)
    {
        var best = EvaluateFiveCards(cards.AsSpan(0, 5).ToArray());
        Span<int> combo = stackalloc int[5];

        for (int i = 0; i < cards.Length - 4; i++)
        {
            combo[0] = i;
            for (int j = i + 1; j < cards.Length - 3; j++)
            {
                combo[1] = j;
                for (int k = j + 1; k < cards.Length - 2; k++)
                {
                    combo[2] = k;
                    for (int l = k + 1; l < cards.Length - 1; l++)
                    {
                        combo[3] = l;
                        for (int m = l + 1; m < cards.Length; m++)
                        {
                            combo[4] = m;
                            var candidate = EvaluateFiveCards(
                                cards[combo[0]],
                                cards[combo[1]],
                                cards[combo[2]],
                                cards[combo[3]],
                                cards[combo[4]]);

                            if (CompareEvaluation(candidate, best) > 0)
                                best = candidate;
                        }
                    }
                }
            }
        }

        return best;
    }

    private static int CompareEvaluation((HandRank Rank, int[] Kickers) left, (HandRank Rank, int[] Kickers) right)
    {
        int rankCompare = left.Rank.CompareTo(right.Rank);
        if (rankCompare != 0) return rankCompare;

        int length = Math.Min(left.Kickers.Length, right.Kickers.Length);
        for (int i = 0; i < length; i++)
        {
            int kickerCompare = left.Kickers[i].CompareTo(right.Kickers[i]);
            if (kickerCompare != 0) return kickerCompare;
        }

        return left.Kickers.Length.CompareTo(right.Kickers.Length);
    }

    private static (HandRank Rank, int[] Kickers) EvaluatePartialCards(Card[] cards)
    {
        Span<int> rankCounts = stackalloc int[14];
        foreach (var card in cards)
            rankCounts[card.Value]++;

        Span<int> quads = stackalloc int[1];
        Span<int> trips = stackalloc int[2];
        Span<int> pairs = stackalloc int[2];
        Span<int> singles = stackalloc int[5];
        BuildRankGroups(rankCounts, quads, trips, pairs, singles,
            out int quadCount, out int tripCount, out int pairCount, out int singleCount);

        return ClassifyRankOnlyHand(quadCount, quads, tripCount, trips, pairCount, pairs, singleCount, singles);
    }

    private static (HandRank Rank, int[] Kickers) EvaluateFiveCards(params Card[] cards)
    {
        Span<int> rankCounts = stackalloc int[14];
        Suit flushSuit = cards[0].Suit;
        bool isFlush = true;

        foreach (var card in cards)
        {
            rankCounts[card.Value]++;
            if (card.Suit != flushSuit)
                isFlush = false;
        }

        int straightHigh = GetStraightHigh(rankCounts);
        bool isStraight = straightHigh > 0;

        if (isFlush && isStraight)
        {
            if (HasBroadwayRanks(rankCounts))
                return (HandRank.RoyalFlush, new[] { 14 });

            return (HandRank.StraightFlush, new[] { straightHigh });
        }

        Span<int> quads = stackalloc int[1];
        Span<int> trips = stackalloc int[2];
        Span<int> pairs = stackalloc int[2];
        Span<int> singles = stackalloc int[5];
        BuildRankGroups(rankCounts, quads, trips, pairs, singles,
            out int quadCount, out int tripCount, out int pairCount, out int singleCount);

        if (quadCount == 1)
            return (HandRank.FourOfAKind, BuildKickers(quads.Slice(0, 1), singles, singleCount, 1));

        if (tripCount == 1 && pairCount >= 1)
            return (HandRank.FullHouse, new[] { trips[0], pairs[0] });

        if (isFlush)
            return (HandRank.Flush, CopyStrengths(singles, singleCount, pairs, pairCount, trips, tripCount));

        if (isStraight)
            return (HandRank.Straight, new[] { straightHigh });

        return ClassifyRankOnlyHand(quadCount, quads, tripCount, trips, pairCount, pairs, singleCount, singles);
    }

    private static void BuildRankGroups(
        Span<int> rankCounts,
        Span<int> quads,
        Span<int> trips,
        Span<int> pairs,
        Span<int> singles,
        out int quadCount,
        out int tripCount,
        out int pairCount,
        out int singleCount)
    {
        quadCount = 0;
        tripCount = 0;
        pairCount = 0;
        singleCount = 0;

        for (int value = 13; value >= 1; value--)
        {
            int count = rankCounts[value];
            if (count == 0) continue;

            int strength = RankStrength(value);
            switch (count)
            {
                case 4:
                    quads[quadCount++] = strength;
                    break;
                case 3:
                    trips[tripCount++] = strength;
                    break;
                case 2:
                    pairs[pairCount++] = strength;
                    break;
                case 1:
                    singles[singleCount++] = strength;
                    break;
            }
        }
    }

    private static (HandRank Rank, int[] Kickers) ClassifyRankOnlyHand(
        int quadCount, Span<int> quads,
        int tripCount, Span<int> trips,
        int pairCount, Span<int> pairs,
        int singleCount, Span<int> singles)
    {
        if (quadCount == 1)
            return (HandRank.FourOfAKind, BuildKickers(quads.Slice(0, 1), singles, singleCount, 1));

        if (tripCount == 1)
            return (HandRank.ThreeOfAKind, BuildKickers(trips.Slice(0, 1), singles, singleCount, 2));

        if (pairCount == 2)
            return (HandRank.Twopair, BuildKickers(pairs.Slice(0, 2), singles, singleCount, 1));

        if (pairCount == 1)
            return (HandRank.Pair, BuildKickers(pairs.Slice(0, 1), singles, singleCount, 3));

        return (HandRank.HighCard, CopyStrengths(singles, singleCount, pairs, pairCount, trips, tripCount));
    }

    private static int[] BuildKickers(Span<int> madeHandRanks, Span<int> singles, int singleCount, int maxExtraKickers)
    {
        int extraKickers = Math.Min(singleCount, maxExtraKickers);
        var kickers = new int[madeHandRanks.Length + extraKickers];
        madeHandRanks.CopyTo(kickers);

        for (int i = 0; i < extraKickers; i++)
            kickers[madeHandRanks.Length + i] = singles[i];

        return kickers;
    }

    private static int[] CopyStrengths(Span<int> singles, int singleCount, Span<int> pairs, int pairCount, Span<int> trips, int tripCount)
    {
        var kickers = new int[singleCount + pairCount + tripCount];
        int index = 0;

        for (int i = 0; i < tripCount; i++) kickers[index++] = trips[i];
        for (int i = 0; i < pairCount; i++) kickers[index++] = pairs[i];
        for (int i = 0; i < singleCount; i++) kickers[index++] = singles[i];

        return kickers;
    }

    private static int GetStraightHigh(Span<int> rankCounts)
    {
        Span<int> ranks = stackalloc int[5];
        int count = 0;

        for (int value = 13; value >= 1; value--)
        {
            if (rankCounts[value] == 0) continue;
            if (++count > 5) return 0;
            ranks[count - 1] = value;
        }

        if (count != 5) return 0;

        if (IsWheel(ranks))
            return 5;

        if (IsBroadway(ranks))
            return 14;

        ranks.Sort();
        for (int i = 1; i < 5; i++)
        {
            if (ranks[i] - ranks[i - 1] != 1)
                return 0;
        }

        return RankStrength(ranks[4]);
    }

    private static bool IsWheel(Span<int> ranks)
    {
        Span<bool> present = stackalloc bool[14];
        foreach (int rank in ranks)
            present[rank] = true;

        return present[1] && present[2] && present[3] && present[4] && present[5];
    }

    private static bool IsBroadway(Span<int> ranks)
    {
        Span<bool> present = stackalloc bool[14];
        foreach (int rank in ranks)
            present[rank] = true;

        return present[1] && present[10] && present[11] && present[12] && present[13];
    }

    private static bool HasBroadwayRanks(Span<int> rankCounts) =>
        rankCounts[1] > 0 && rankCounts[10] > 0 && rankCounts[11] > 0 && rankCounts[12] > 0 && rankCounts[13] > 0;

    private static int RankStrength(int value) => value == 1 ? 14 : value;
}
