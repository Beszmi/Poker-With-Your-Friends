using System;
using System.Collections.Generic;

namespace Poker_With_Your_Friends.Model
{
    public class Deck
    {
        private List<Card> cards = new List<Card>();

        public List<Card> Cards { get { return cards; } }
        public Deck() {
            cards = new List<Card>();
            for (int i = 1; i <= 13; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    cards.Add(new Card(i, j));
                }
            }
        }

        public void Shuffle()
        {
            Random rng = new Random();
            int n = cards.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                Card value = cards[k];
                cards[k] = cards[n];
                cards[n] = value;
            }
        }

        public Card DrawCard()
        {
            if (cards.Count == 0)
            {
                throw new InvalidOperationException("The deck is empty.");
            }
            Card drawnCard = cards[cards.Count - 1];
            cards.RemoveAt(cards.Count - 1);
            return drawnCard;
        }
    }
}
