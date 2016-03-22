using System;
using System.Collections.Generic;

namespace PokerService.Cards
{
    public struct Card
    {
        public readonly FlushType Flush;

        public readonly NumberType Number;

        public Card(FlushType flush, NumberType number)
        {
            Flush = flush;
            Number = number;
        }

        static Dictionary<char, FlushType> ch2flush = new Dictionary<char, FlushType>
        {
            {'c', FlushType.Clubs},
            {'d', FlushType.Diamonds},
            {'h', FlushType.Hearts},
            {'s', FlushType.Spades},
            {'♣', FlushType.Clubs},
            {'♦', FlushType.Diamonds},
            {'♥', FlushType.Hearts},
            {'♠', FlushType.Spades},
        };
        static char[] ch2number = new[]
            {
                '2', '3', '4', '5', '6', '7', '8', '9', 't', 'j', 'q', 'k', 'a'
            };
        public static Card Parse(string value)
        {
            value = value.ToLower();
            if (value == "j")
                return new Card(FlushType.Joker, 0);
            if (value.Length == 3 && value.Substring(1) == "10")
                return new Card(ch2flush[value[0]], NumberType._10);
            else
                return new Card(ch2flush[value[0]], (NumberType)Array.IndexOf(ch2number, value[1]));
        }

        static readonly char[] flush2chr = new[] { '♣', '♦', '♥', '♠' };
        public override string ToString()
        {
            return Flush == FlushType.Joker ? "J" : flush2chr[(int)Flush] + ch2number[(byte)Number].ToString().ToUpper();
        }

        public string ToString2()
        {
            return Flush == FlushType.Joker ? "J" : Flush.ToString()[0] + ch2number[(byte)Number].ToString().ToUpper();
        }
    }
}
