using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.Model
{
    public enum Suit
    {
        Spade,
        Diamond,
        Heart,
        Club
    }
    public class Card
    {
        private int value;

        public int Value
        {
            get { return value; }
            set { this.value = value; }
        }

        private Suit suit;

        public Suit Suit
        {
            get { return suit; }
            set { this.suit = value; }
        }

        public Card(int value, Suit suit)
        {
            this.value = value;
            this.suit = suit;
        }

        public Card(int value, int suit)
        {
            this.value = value;
            switch (suit)
            {
                case 0:
                    this.suit = Suit.Spade;
                    break;
                case 1:
                    this.suit = Suit.Diamond;
                    break;
                case 2:
                    this.suit = Suit.Heart;
                    break;
                case 3:
                    this.suit = Suit.Club;
                    break;
                default:
                    throw new ArgumentException("Invalid suit value");
            }

        }
        public String SuitSymbol
        {
            get
            {
                return suit switch
                {
                    Suit.Spade => "♠",
                    Suit.Diamond => "♦",
                    Suit.Heart => "♥",
                    Suit.Club => "♣",
                    _ => throw new ArgumentException("Invalid suit value"),
                };
            }
        }
        public String ValueSymbol
        {
            get
            {
                return value switch
                {
                    1 => "A",
                    2 => "2",
                    3 => "3",
                    4 => "4",
                    5 => "5",
                    6 => "6",
                    7 => "7",
                    8 => "8",
                    9 => "9",
                    10 => "10",
                    11 => "J",
                    12 => "Q",
                    13 => "K",
                    _ => throw new ArgumentException("Invalid value"),
                };
            }
        }

        public String Color
        {
            get
            {
                return suit switch
                {
                    Suit.Spade => "Black",
                    Suit.Club => "Black",
                    Suit.Diamond => "Red",
                    Suit.Heart => "Red",
                    _ => throw new ArgumentException("Invalid suit value"),
                };
            }
        }
        public override String ToString()
        {
            return $"{ValueSymbol}{SuitSymbol}";
        }
    }
}
