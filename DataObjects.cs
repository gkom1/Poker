using Newtonsoft.Json;
using PokerService.Cards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokerService
{
    public struct TableJson
    {
        [JsonProperty("players")]
        public string[][][] Players { get; set; }

        [JsonProperty("cards")]
        public string[][] Common { get; set; }
    }

    public class CountJson
    {
        [JsonProperty("players")]
        public Dictionary<string, string> PlayersCoefficients { get; set; }

        [JsonProperty("combinations")]
        public Dictionary<string, string> CombinationCoefficients { get; set; }

        [JsonProperty("express")]
        public Dictionary<string, Dictionary<string, string>> ExpressCoefficients { get; set; }

        public CountJson()
        {
            PlayersCoefficients = new Dictionary<string, string>();
            CombinationCoefficients = new Dictionary<string, string>();
            ExpressCoefficients = new Dictionary<string, Dictionary<string, string>>();
        }
    }

    public class ComboCheckJson
    {
        [JsonProperty("players")]
        public Dictionary<string, int[]> PlayersCombinations { get; set; }

        [JsonProperty("combo")]
        public string Combination { get; set; }
    }

    public static class Utils
    {
        private static Dictionary<string, NumberType> _stringToNumberType = new Dictionary<string, NumberType> {
                {"2", NumberType._2 },
                {"3", NumberType._3 },
                {"4", NumberType._4 },
                {"5", NumberType._5 },
                {"6", NumberType._6 },
                {"7", NumberType._7 },
                {"8", NumberType._8 },
                {"9", NumberType._9 },
                {"10", NumberType._10 },
                {"J", NumberType.J },
                {"Q", NumberType.Q },
                {"K", NumberType.K },
                {"A", NumberType.A },
            };

        private static Dictionary<string, FlushType> _stringToFlushType = new Dictionary<string, FlushType> {
                {"C", FlushType.Clubs },
                {"D", FlushType.Diamonds },
                {"H", FlushType.Hearts },
                {"S", FlushType.Spades }
            };

        public static Card[] ToCards(this string[][] text)
        {
            return text.Select(c => new Card(_stringToFlushType[c[1]], _stringToNumberType[c[0]])).ToArray();
        }

        public static Card[][] ToCards(this string[][][] text)
        {
            return text.Select(x => x.ToCards()).ToArray();
        }

        public static string[][] MakeStrings(this Card[] cards)
        {
            return cards.Select(c =>
                new[]{
                    _stringToNumberType.First(x => x.Value == c.Number).Key,
                    _stringToFlushType.First(x => x.Value == c.Flush).Key
                }).ToArray();
        }
        public static string[][][] MakeStrings(this Card[][] cards)
        {
            return cards.Select(x => x.MakeStrings()).ToArray();
        }
    }
}
