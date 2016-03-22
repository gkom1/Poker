using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PokerService.Cards
{
    public class CardSet
    {
        private const int CARDS_COUNT = 7;
        public const int COMMON_CARDS_COUNT = 5;

        int _index;
        Card[] _cards;
        private NumberType[] _kicker;

        public static int[] Powers52 { get; private set; }
        public static int[] Powers13 { get; private set; }
        public static int[] Powers6 { get; private set; }
        public static int[] Powers5 { get; private set; }
        public static ushort[] FromTable { get; private set; }

        public CombinationType Type { get; private set; }

        static CardSet()
        {

            Powers13 = new int[COMMON_CARDS_COUNT + 1];
            Powers52 = new int[COMMON_CARDS_COUNT + 1];
            for (int i = 0; i < COMMON_CARDS_COUNT + 1; i++)
            {
                Powers13[i] = (int)Math.Pow(13, COMMON_CARDS_COUNT - i);
                Powers52[i] = (int)Math.Pow(52, COMMON_CARDS_COUNT - i);
            }
            Powers6 = new int[5]; // от 0 до 5 карт одной масти
            for (int i = 0; i < 5; i++)
                Powers6[i] = (int)Math.Pow(6, 4 - i);
            Powers5 = new int[14]; // от 0 до 4 карт одного достоинства
            for (int i = 0; i < 14; i++)
                Powers5[i] = (int)Math.Pow(5, 13 - i);
        }

        // загрузка таблицы комбинаций из файла
        public static void Init(string tableFileName)
        {
            byte[] buffer = File.ReadAllBytes(tableFileName);

            ushort[] shorts = new ushort[buffer.Length / 2];
            for (int i = 0; i < shorts.Length; i++)
                shorts[i] = (ushort)(buffer[i * 2] + buffer[i * 2 + 1] * 256);

            FromTable = shorts;

            //Kickers = new ushort[buffer.Length / 2];
            //Values = new CombinationType[buffer.Length / 2];
            //Parallel.For(0, shorts.Length, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, i =>
            //{
            //    Values[i] = (CombinationType)(shorts[i] >> 12);
            //    Kickers[i] = (ushort)(shorts[i] & 4095);
            //});
        }

        private void IsFlush(NumberType[] kickers)
        {
            var newKickers = kickers;
            Array.Sort(newKickers);
            Array.Reverse(newKickers);

            if (Type != CombinationType.STRAIGHT)
            {
                _kicker = newKickers.Take(5).ToArray();
                Type = CombinationType.FLUSH;
            }
            else
            {
                bool isStraight = false;
                for (int i = 0; i <= newKickers.Length - COMMON_CARDS_COUNT; i++)
                    if (isStraight |= ((int)newKickers[i] == (int)newKickers[i + 4] + 4))
                    {
                        if (newKickers[i] == NumberType.A)
                            Type = CombinationType.ROYAL_FLUSH;
                        else
                            Type = CombinationType.STRAIGHT_FLUSH;
                        _kicker = new[] { newKickers[i] };
                        break;
                    }

                if (!isStraight)
                    if (newKickers[0] == NumberType.A && newKickers[newKickers.Length - 4] == NumberType._5 && newKickers[newKickers.Length - 1] == NumberType._2)
                    {
                        Type = CombinationType.STRAIGHT_FLUSH;
                        _kicker = new[] { NumberType._5 };
                    }
                    else
                    {
                        _kicker = newKickers.Take(5).ToArray();
                        Type = CombinationType.FLUSH;
                    }
            }
        }

        public CardSet(Card[] cards, bool canBeFlush)
        {
            if (cards.Length != CARDS_COUNT || cards[0].Number > cards[1].Number)
                throw new Exception();

            _cards = cards;

            _index = Powers13[0] * (int)_cards[0].Number * (25 - (int)_cards[0].Number) / 2;
            for (int i = 1; i < CARDS_COUNT; i++)
                _index += (int)_cards[i].Number * Powers13[i - 1];

            Type = (CombinationType)(FromTable[_index] >> 12);
            _kicker = new[] { (NumberType)((FromTable[_index] >> 8) & 15), (NumberType)((FromTable[_index] >> 4) & 15), (NumberType)(FromTable[_index] & 15) };

            if(canBeFlush)
            {
                var flushes = _cards.GroupBy(c => c.Flush);
                if (flushes.Any(g => g.Count() >= 5))
                    IsFlush(flushes.First(g => g.Count() >= 5).Select(c => c.Number).OrderByDescending(n => n).ToArray());
            }
        }

        public CardSet(int index, Card[] cards, bool isFlush, NumberType[] kickers)
        {
            if (cards.Length != 2 || cards[0].Number > cards[1].Number)
                throw new Exception();

            _cards = cards;
            _index = index;
            Type = (CombinationType)(FromTable[_index] >> 12);

            if (!isFlush)
                _kicker = new[] { (NumberType)((FromTable[_index] >> 8) & 15), (NumberType)((FromTable[_index] >> 4) & 15), (NumberType)(FromTable[_index] & 15) };
            else
                IsFlush(kickers);
        }

        public CardSet(string value, bool canBeFlush)
            : this(value.Split(new[]{','}, StringSplitOptions.RemoveEmptyEntries).Select(x => Card.Parse(x.Trim())).ToArray(), canBeFlush)
        {

        }

        public static int Compare(CardSet c1, CardSet c2)
        {
            int value = c1.Type.CompareTo(c2.Type);

            if (value != 0)
                return value;
            else
            {
                if (c1.Type == CombinationType.FLUSH)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        value = c1._kicker[i].CompareTo(c2._kicker[i]);
                        if (value != 0)
                            break;
                    }
                    return value;
                }
                else
                {
                    value = c1._kicker[0].CompareTo(c2._kicker[0]);
                    if (value != 0)
                        return value;
                    else if (c1.Type != CombinationType.STRAIGHT && c1.Type != CombinationType.STRAIGHT_FLUSH && c1.Type != CombinationType.ROYAL_FLUSH)
                    {
                        value = c1._kicker[1].CompareTo(c2._kicker[1]);
                        if (value != 0)
                            return value;
                        else if (c1.Type != CombinationType.FOUR && c1.Type != CombinationType.FULL_HOUSE)
                        {
                            value = c1._kicker[2].CompareTo(c2._kicker[2]);
                            if (value != 0)
                                return value;
                            else if (c1.Type != CombinationType.THREE && c1.Type != CombinationType.PAIR2)
                            {
                                Func<int, NumberType[]> getCards = x => Enumerable.Range(0, 5).Select(i => (NumberType)((x % Powers13[i]) / Powers13[i + 1])).ToArray();

                                NumberType[] nc1 = c1._cards.Select(c => c.Number).Union(getCards(c1._index)).Where(c => !c1._kicker.Contains(c)).ToArray();
                                NumberType[] nc2 = c2._cards.Select(c => c.Number).Union(getCards(c2._index)).Where(c => !c2._kicker.Contains(c)).ToArray();
                                Array.Sort(nc1);
                                Array.Sort(nc2);

                                value = nc1[nc1.Length - 1].CompareTo(nc2[nc2.Length - 1]);
                                if (value == 0 && c1.Type == CombinationType.NONE)
                                    value = nc1[nc1.Length - 2].CompareTo(nc2[nc2.Length - 2]);

                                return value;
                            }
                        }
                    }
                }
            }
            return value;
        }

        public static CombinationType GetFinalCombinations(CardSet[] sets, out Dictionary<int, Card[]> resultCards)
        {
            Card[] common = sets[0]._cards.Intersect(sets[1]._cards).ToArray();

            CardSet max = sets[0];
            for (int i = 1; i < sets.Length; i++)
                if (CardSet.Compare(max, sets[i]) < 0)
                    max = sets[i];

            int[] maxes = sets.Where(s => CardSet.Compare(s, max) == 0).Select(s => Array.IndexOf(sets, s)).ToArray();

            resultCards = new Dictionary<int, Card[]>();
            foreach (int i in maxes)
            {
                if (sets[i]._cards.Length != CARDS_COUNT)
                    throw new Exception();

                switch (sets[i].Type)
                {
                    default:
                        resultCards.Add(i, sets[i]._cards.OrderByDescending(c => c.Number).Take(5).ToArray());
                        break;
                    case CombinationType.PAIR:
                        var pair = sets[i]._cards.GroupBy(c => c.Number).OrderByDescending(c => c.Key).First(g => g.Count() == 2);
                        resultCards.Add(i, sets[i]._cards.Where(c => c.Number == pair.Key).Union(sets[i]._cards.Where(c => c.Number != pair.Key).OrderByDescending(c => c.Number).Take(3)).ToArray());
                        break;
                    case CombinationType.PAIR2:
                        var pairs = sets[i]._cards.GroupBy(c => c.Number).Where(g => g.Count() == 2).OrderByDescending(c => c.Key).Take(2).ToArray();
                        resultCards.Add(i, sets[i]._cards.Where(c => c.Number == pairs[0].Key).Union(sets[i]._cards.Where(c => c.Number == pairs[1].Key)).Union(sets[i]._cards.Where(c => c.Number != pairs[0].Key && c.Number != pairs[1].Key).OrderByDescending(c => c.Number).Take(1)).ToArray());
                        break;
                    case CombinationType.THREE:
                        var three = sets[i]._cards.GroupBy(c => c.Number).OrderByDescending(c => c.Key).First(g => g.Count() == 3);
                        resultCards.Add(i, sets[i]._cards.Where(c => c.Number == three.Key).Union(sets[i]._cards.Where(c => c.Number != three.Key).OrderByDescending(c => c.Number).Take(2)).ToArray());
                        break;
                    case CombinationType.STRAIGHT: // с учетом того, что какое-то из достоинств может быть и на столе, и на руках
                        bool isFound = false;
                        var numbers = sets[i]._cards.GroupBy(c => c.Number).OrderByDescending(g => g.Key).ToArray();
                        for (int j = 0; j < numbers.Length - 4; j++)
                        {
                            var sequence = numbers.Skip(j).Take(5).ToArray();
                            if ((int)sequence[0].Key - 4 == (int)sequence[4].Key)
                            {
                                resultCards.Add(i, sequence.Select(g => g.OrderByDescending(c => common.Contains(c) ? 1 : 0).First()).ToArray());
                                isFound = true;
                                break;
                            }
                        }
                        if (!isFound)
                            if (numbers[0].Key == NumberType.A && numbers[numbers.Length - 4].Key == NumberType._5)
                                resultCards.Add(i, new Card[] { numbers[0].First(), numbers[numbers.Length - 4].First(), numbers[numbers.Length - 3].First(), numbers[numbers.Length - 2].First(), numbers[numbers.Length - 1].First() });
                        break;
                    case CombinationType.FLUSH:
                        resultCards.Add(i, sets[i]._cards.GroupBy(c => c.Flush).First(g => g.Count() >= 5).OrderByDescending(c => c.Number).Take(5).ToArray());
                        break;
                    case CombinationType.FULL_HOUSE: // с учетом того, что какое-то из достоинств может быть и на столе, и на руках
                        var fhThree = sets[i]._cards.GroupBy(c => c.Number).OrderByDescending(c => c.Key).First(g => g.Count() == 3);
                        var fhPair = sets[i]._cards.Where(c => c.Number != fhThree.Key).GroupBy(c => c.Number).OrderByDescending(c => c.Key).First(g => g.Count() >= 2 && g.Count() <= 3);
                        resultCards.Add(i, sets[i]._cards.Where(c => c.Number == fhThree.Key).Union(sets[i]._cards.Where(c => c.Number == fhPair.Key).OrderByDescending(c => common.Contains(c) ? 1 : 0).Take(2)).ToArray());
                        break;
                    case CombinationType.FOUR:
                        var four = sets[i]._cards.GroupBy(c => c.Number).OrderByDescending(c => c.Key).First(g => g.Count() == 4);
                        resultCards.Add(i, sets[i]._cards.Where(c => c.Number == four.Key).Union(sets[i]._cards.Where(c => c.Number != four.Key).OrderByDescending(c => c.Number).Take(1)).ToArray());
                        break;
                    case CombinationType.STRAIGHT_FLUSH:
                        bool isFoundSF = false;
                        var sfNumbers = sets[i]._cards.GroupBy(c => c.Flush).First(g => g.Count() >= 5).OrderByDescending(c => c.Number).ToArray(); // все карты нужной масти, отсортированные по убыванию
                        for (int j = 0; j < sfNumbers.Count() - 4; j++)
                        {
                            var sequence = sfNumbers.Skip(j).Take(5).ToArray();
                            if ((int)sequence[0].Number == (int)sequence[4].Number + 4) // если есть 5 идущих по порядку карт
                            {
                                resultCards.Add(i, sequence);
                                isFoundSF = true;
                                break;
                            }
                        }
                        if (!isFoundSF) // если нет 5 идущих по порядку карт
                            if (sfNumbers[0].Number == NumberType.A && sfNumbers[sfNumbers.Length - 4].Number == NumberType._5) // если стрит-флэш на 5, 4, 3, 2, A
                                resultCards.Add(i, new Card[] { sfNumbers[0], sfNumbers[sfNumbers.Length - 4], sfNumbers[sfNumbers.Length - 3], sfNumbers[sfNumbers.Length - 2], sfNumbers[sfNumbers.Length - 1] });
                        break;
                    case CombinationType.ROYAL_FLUSH:
                        resultCards.Add(i, sets[i]._cards.GroupBy(c => c.Flush).First(g => g.Count() >= 5).OrderByDescending(c => c.Number).Take(5).ToArray());
                        break;
                }
            }

            return sets[maxes[0]].Type;
        }
    }
}