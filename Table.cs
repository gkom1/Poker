using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokerService.Cards
{
    public class Table
    {
        const int COMBINATIONS_COUNT = 10;
        const int COMMON_CARDS_COUNT = 5;

        double[] increment;

        bool[,] _used = new bool[13, 4];
        int[] _numbersOnHands;
        int[,] _flushesCounts;
        Card[][] _players;
        int[] _playerIndices;

        public double[][] Counts { get; private set; }

        public Table(Card[][] players, bool exactMode = true)
        {
            if (exactMode)
                increment = Enumerable.Repeat(1.0, players.Length + 1).ToArray();
            else
                increment = Enumerable.Range(0, players.Length + 1).Select(x => x < 2 ? 1 : (x + 1.0) / x).ToArray();

            _players = players;
            _playerIndices = new int[_players.Length];
            for (int i = 0; i < _players.Length; i++)
            {
                _players[i] = _players[i].OrderBy(c => c.Number).ToArray();
                _playerIndices[i] = ((int)_players[i][0].Number * (25 - (int)_players[i][0].Number) / 2 + (int)_players[i][1].Number) * CardSet.Powers13[0];
            }

            _flushesCounts = new int[4, _players.Length];
            for (byte i = 0; i < 4; i++)
                for (int j = 0; j < _players.Length; j++)
                    _flushesCounts[i, j] = _players[j].Count(c => c.Flush == (FlushType)i);

            _numbersOnHands = new int[13];
            Parallel.For(0, 13, i =>
            {
                _numbersOnHands[i] = _players.Sum(p => p.Count(c => c.Number == (NumberType)i));

                for (int j = 0; j < 4; j++)
                {
                    _used[i, j] = false;
                    foreach (Card[] p in _players)
                        _used[i, j] |= p.Contains(new Card((FlushType)j, (NumberType)i));
                }
            });

            Counts = new double[_players.Length+1][];
            for (int i = 0; i <= _players.Length; i++)
                Counts[i] = new double[COMBINATIONS_COUNT];

            //DrawCounts = new int[_players.Length][];
            //for (int i = 0; i < _players.Length; i++)
            //    DrawCounts[i] = new int[_players.Length - 1];
        }

        private void GetCountsInternal(int rawIndex, int netIndex, int flushMask)//, int cutLevel)
        {
            CardSet[] sets = new CardSet[_players.Length];

            int maxFlush = -1, maxFlushCount = 0;
            if (flushMask > 0)
            {
                int curFlushCount = 0;
                for (int j = 0; j < 4; j++)
                {
                    curFlushCount = (flushMask % CardSet.Powers6[j]) / CardSet.Powers6[j + 1];
                    if (curFlushCount >= 3)
                    {
                        maxFlushCount = curFlushCount;
                        maxFlush = j;
                    }
                }
            }

            bool isFlush;
            int n, kCounter = 0;
            NumberType[] numbers;
            for (int i = 0; i < _players.Length; i++)
            {
                isFlush = false;
                numbers = null;
                if (flushMask > 0 && maxFlush >= 0)
                    if (isFlush = _flushesCounts[maxFlush, i] + maxFlushCount >= 5)
                    {
                        kCounter = 0;
                        numbers = new NumberType[_flushesCounts[maxFlush, i] + maxFlushCount];
                        for (int k = 0; k < _players[i].Length; k++)
                            if (_players[i][k].Flush == (FlushType)maxFlush)
                                numbers[kCounter++] = _players[i][k].Number;
                        for (int k = 0; k < COMMON_CARDS_COUNT; k++)
                        {
                            n = ((rawIndex % CardSet.Powers52[k]) / CardSet.Powers52[k + 1]);
                            if ((n & 3) == maxFlush)
                                numbers[kCounter++] = (NumberType)(n >> 2);
                        }
                        Array.Sort(numbers);
                        Array.Reverse(numbers);
                    }

                sets[i] = new CardSet(_playerIndices[i] + netIndex, _players[i], isFlush, numbers);
            }

            List<Tuple<int, CardSet>> maxes = new List<Tuple<int, CardSet>>();
            maxes.Add(Tuple.Create(0, sets[0]));
            int result = 0;
            for (int i = 1; i < sets.Length; i++)
            {
                result = CardSet.Compare(maxes[0].Item2, sets[i]);
                if (result < 0)
                {
                    maxes.Clear();
                    maxes.Add(Tuple.Create(i, sets[i]));
                }
                else if (result == 0)
                    maxes.Add(Tuple.Create(i, sets[i]));
            }

            lock (Counts)
            {
                double inc = increment[maxes.Count];
                foreach (var max in maxes)
                    Counts[max.Item1][(int)max.Item2.Type] += inc;
                Counts[_players.Length][(int)maxes[0].Item2.Type] += inc;
            }
        }

        public void GetCounts(int level, int rawIndex, int netIndex, int lastCard, int iStart, int numberMask, int flushMask)
        {
            int newLastCard = 0, newNumberMask = 0;
            int[] used = new int[level];
            int[] numbers = new int[13];

            for (int i = 0; i < level; i++)
                used[i] = (rawIndex % CardSet.Powers52[i]) / CardSet.Powers52[i + 1];

            if (level == 0 || numberMask == 0)
                numbers[lastCard >> 2]++;
            else
                for (int i = 0; i < 13; i++)
                    numbers[i] = _numbersOnHands[i] + (numberMask % CardSet.Powers5[i]) / CardSet.Powers5[i + 1];

            rawIndex += lastCard * CardSet.Powers52[level + 1];
            netIndex += iStart * CardSet.Powers13[level + 1];

            for (int i = iStart; i < 13; i++)
                if (numbers[i] < 4)
                {
                    newNumberMask = numberMask + CardSet.Powers5[i + 1];
                        for (int j = 0; j < 4; j++)
                        {
                            newLastCard = (i << 2) | j;
                            if (newLastCard > lastCard && !_used[i, j] && !used.Contains(newLastCard))
                            {
                                if (level + 2 < COMMON_CARDS_COUNT)
                                    GetCounts(level + 1, rawIndex, netIndex, newLastCard, i, newNumberMask, flushMask + CardSet.Powers6[j + 1]);//, -1);
                                else
                                    GetCountsInternal(rawIndex + newLastCard, netIndex + i, flushMask + CardSet.Powers6[j + 1]);
                            }
                        }
                }
        }

        public void CountPreflop()
        {
            FlushType[] flushes = Enumerable.Range(0, 4).Select(x => (FlushType)(byte)x).ToArray();

            Func<double[][]> make_result = () =>
                Enumerable
                .Range(0, _players.Length+1)
                .Select(x => new double[COMBINATIONS_COUNT])
                .ToArray();

            var deck = Enumerable
                .Range(0, 13)
                .SelectMany(num => flushes.Select(f => new Card(f, (NumberType)num)))
                .Except(_players.SelectMany(p => p))
                .ToArray();

            var pflushes = _players
                .Select(p => flushes.Select(f => p.Count(c => c.Flush == f)).ToArray())
                .ToArray();

            int N = deck.Length;
            int M = N - 4;
            Parallel.For(0, (M + 1) * M / 2, i12 =>
                {
                    ulong[] card_sets = new ulong[_players.Length];
                    Card[] card_all = new Card[5];
                    ushort[] costs = new ushort[_players.Length];
                    int[] arg_max = new int[_players.Length];
                    var possible_flush = new FlushType[_players.Length];
                    var always_flush = new bool[_players.Length];
                    NumberType[] straight_cards = new NumberType[7];

                    int i1, i2 = 0;
                    for (i1 = 0; i1 < M; ++i1)
                        if (i12 < M - i1)
                        {
                            i2 = i12 + i1;
                            break;
                        }
                        else
                            i12 -= M - i1;
                    ++i2;
                    var r = make_result();
                    card_all[0] = deck[i1];
                    card_all[1] = deck[i2];
                    int I2 = CardSet.Powers13[1] * (int)card_all[0].Number + CardSet.Powers13[2] * (int)card_all[1].Number;
                    var f = pflushes.Select(pf =>
                        {
                            var arr = (int[])pf.Clone();
                            ++arr[(int)card_all[0].Flush];
                            ++arr[(int)card_all[1].Flush];
                            return arr;
                        }).ToArray();

                    for (int i3 = i2+1; i3 < N-2; ++i3)
                    {
                        card_all[2] = deck[i3];
                        int I3 = I2 + CardSet.Powers13[3] * (int)card_all[2].Number;
                        for (int i = 0; i < f.Length; ++i)
                            ++f[i][(int)card_all[2].Flush];
                        for (int i4 = i3+1; i4 < N-1; ++i4)
                        {
                            card_all[3] = deck[i4];
                            int I4 = I3 + CardSet.Powers13[4] * (int)card_all[3].Number;
                            for (int i = 0; i < possible_flush.Length; ++i)
                            {
                                always_flush[i] = false;
                                possible_flush[i] = FlushType.Joker;
                                for (int j = 0; j < flushes.Length; ++j)
                                    if (f[i][j] >= 5 || flushes[j] == card_all[3].Flush && f[i][j] == 4)
                                    {
                                        always_flush[i] = true;
                                        possible_flush[i] = flushes[j];
                                        break;
                                    }
                                    else if (f[i][j] == 4 || f[i][j] == 3 && flushes[j] == card_all[3].Flush)
                                    {
                                        possible_flush[i] = flushes[j];
                                        break;
                                    }
                            }

                            for (int i5 = i4 + 1; i5 < N; ++i5)
                            {
                                var c5 = deck[i5];
                                card_all[4] = c5;
                                int I5 = I4 + (int)c5.Number;
                                for (int p = 0; p < _playerIndices.Length; ++p)
                                {
                                    var cost = CardSet.FromTable[_playerIndices[p] + I5];
                                    var comb = (CombinationType)(cost >> 12);
                                    bool isFlush = always_flush[p] || possible_flush[p] == c5.Flush;
                                    if (isFlush)
                                    {
                                        if (comb == CombinationType.STRAIGHT)
                                        {
                                            int straight_cards_count = 0;
                                            Card cp1 = _players[p][0], cp2 = _players[p][1];
                                            var pf = possible_flush[p];
                                            int i = 0;
                                            if (cp1.Flush == pf)
                                            {
                                                for (; i < 5 && card_all[i].Number < cp1.Number; ++i)
                                                    if (card_all[i].Flush == pf)
                                                    {
                                                        straight_cards[straight_cards_count] = card_all[i].Number;
                                                        ++straight_cards_count;
                                                    }
                                                straight_cards[straight_cards_count] = cp1.Number;
                                                ++straight_cards_count;
                                            }
                                            if (cp2.Flush == pf)
                                            {
                                                for (; i < 5 && card_all[i].Number < cp2.Number; ++i)
                                                    if (card_all[i].Flush == pf)
                                                    {
                                                        straight_cards[straight_cards_count] = card_all[i].Number;
                                                        ++straight_cards_count;
                                                    }
                                                straight_cards[straight_cards_count] = cp2.Number;
                                                ++straight_cards_count;
                                            }
                                            for (; i <5; ++i)
                                                if (card_all[i].Flush == pf)
                                                {
                                                    straight_cards[straight_cards_count] = card_all[i].Number;
                                                    ++straight_cards_count;
                                                }
                                            if (straight_cards_count >= 5)
                                            {
                                                bool isSF = false;
                                                for (i = straight_cards_count-5; i >=0; --i)
                                                    if (straight_cards[i + 4] == straight_cards[i] + 4)
                                                    {
                                                        isSF = true;
                                                        cost = (ushort)((int)straight_cards[i + 4] << 8);
                                                        break;
                                                    }
                                                if (!isSF && straight_cards[0] == NumberType._2 && straight_cards[3] == NumberType._5 && straight_cards[straight_cards_count - 1] == NumberType.A)
                                                {
                                                    isSF = true;
                                                    cost = (ushort)(((int)NumberType._5) << 8);
                                                }

                                                if (isSF)
                                                {
                                                    if (straight_cards[straight_cards_count - 1] == NumberType.A
                                                        && straight_cards[straight_cards_count - 5] == NumberType._10)
                                                        cost = (ushort)((((ushort)CombinationType.ROYAL_FLUSH) << 12) | (cost & 0x0FFF));
                                                    else
                                                        cost = (ushort)((((ushort)CombinationType.STRAIGHT_FLUSH) << 12) | (cost & 0x0FFF));
                                                }
                                                else
                                                    cost = ((ushort)CombinationType.FLUSH) << 12;
                                            }
                                            else
                                                cost = ((ushort)CombinationType.FLUSH) << 12;
                                        }
                                        else
                                            cost = ((ushort)CombinationType.FLUSH) << 12;
                                    }
                                    costs[p] = cost;
                                }
                                var max_cost = costs[0];
                                int arg_max_count = 1;
                                arg_max[0] = 0;
                                for (int p = 1; p < costs.Length; ++p)
                                    if (costs[p] > max_cost)
                                    {
                                        max_cost = costs[p];
                                        arg_max[0] = p;
                                        arg_max_count = 1;
                                    }
                                    else if (costs[p] == max_cost)
                                    {
                                        arg_max[arg_max_count] = p;
                                        ++arg_max_count;
                                    }
                                var max_comb = (CombinationType)(max_cost >> 12);

                                double inc = increment[arg_max_count];
                                if (arg_max_count == 1)
                                {
                                    r[arg_max[0]][(int)max_comb] += inc;
                                    r[_players.Length][(int)max_comb] += inc;
                                    continue;
                                }
                                switch (max_comb)
                                {
                                    case CombinationType.PAIR:
                                    case CombinationType.NONE:
                                    case CombinationType.FLUSH:
                                        break;
                                    default:
                                        for (int i = 0; i < arg_max_count; i++)
                                            r[arg_max[i]][(int)max_comb] += inc;
                                        r[_players.Length][(int)max_comb] += inc;
                                        continue;
                                }
                                
                                for (int p = 0; p < card_sets.Length; ++p)
                                    card_sets[p] = 0;

                                if (max_comb == CombinationType.PAIR)
                                {
                                    NumberType pair = (NumberType)((max_cost >> 8) & 0xF);
                                    for (int p = 0; p < arg_max_count; ++p )
                                        card_sets[arg_max[p]] = _players[arg_max[p]].Concat(card_all)
                                                .Select(c => c.Number)
                                                .Where(x => x != pair)
                                                .OrderByDescending(x => x)
                                                .Take(3)
                                                .Aggregate(0ul, (acc, n) => (acc << 4) + (ulong)n);
                                }
                                else if (max_comb == CombinationType.NONE)
                                {
                                    for (int p = 0; p < arg_max_count; ++p)
                                        card_sets[arg_max[p]] = _players[arg_max[p]].Concat(card_all)
                                                .Select(c => c.Number)
                                                .OrderByDescending(x => x)
                                                .Take(5)
                                                .Aggregate(0ul, (acc, n) => (acc << 4) + (ulong)n);
                                }
                                else
                                {
                                    var flush = possible_flush[arg_max[0]];
                                    int flush_count = 0;
                                    NumberType min_common = NumberType.A;
                                    for (int i = 0; i < 5; ++i )
                                        if(card_all[i].Flush == flush)
                                        {
                                            ++flush_count;
                                            if (card_all[i].Number < min_common)
                                                min_common = card_all[i].Number;
                                        }
                                    if (flush_count == 5)
                                    {
                                        for (int p = 0; p < arg_max_count; ++p)
                                        {
                                            var pp = _players[arg_max[p]];
                                            if(pp[1].Flush == flush)
                                            {
                                                if (pp[1].Number > min_common)
                                                    card_sets[arg_max[p]] = (ulong)pp[1].Number;
                                            }
                                            else if (pp[0].Flush == flush && pp[0].Number > min_common)
                                                card_sets[arg_max[p]] = (ulong)pp[0].Number;
                                        }
                                    }else
                                        for (int p = 0; p < arg_max_count; ++p)
                                        {
                                            var pp = _players[arg_max[p]];
                                            if (pp[1].Flush == flush)
                                                    card_sets[arg_max[p]] = (ulong)pp[1].Number;
                                            else
                                                card_sets[arg_max[p]] = (ulong)pp[0].Number;
                                        }
                                }

                                arg_max[0] = 0;
                                arg_max_count = 1;
                                for (int p = 1; p < card_sets.Length; ++p)
                                    if (card_sets[p] > card_sets[arg_max[0]])
                                    {
                                        arg_max[0] = p;
                                        arg_max_count = 1;
                                    }
                                    else if (card_sets[p] == card_sets[arg_max[0]])
                                    {
                                        arg_max[arg_max_count] = p;
                                        ++arg_max_count;
                                    }

                                inc = increment[arg_max_count];
                                for (int i = 0; i < arg_max_count; ++i)
                                    r[arg_max[i]][(int)max_comb] += inc;
                                r[_players.Length][(int)max_comb] += inc;
                            }
                        }
                        for (int i = 0; i < f.Length; ++i)
                            f[i][(int)card_all[2].Flush]--;
                    }

                    lock (Counts)
                    {
                        for (int i = 0; i <= _players.Length; ++i)
                            for (int j = 0; j < COMBINATIONS_COUNT; ++j)
                                Counts[i][j] += r[i][j];
                    }
                });
        }

        public void GetProbabilities(Card[] common)
        {
            if (common.Length == 0)
                CountPreflop();
            else
            {
                int rawIndex = 0, netIndex = 0, flushMask = 0, numberMask = 0;

                for (int i = 0; i < common.Length; i++)
                {
                    rawIndex += ((int)common[i].Number * 4 + (int)common[i].Flush) * CardSet.Powers52[i + 1];
                    netIndex += (int)common[i].Number * CardSet.Powers13[i + 1];
                }

                var numbers = common.GroupBy(c => c.Number).OrderBy(g => g.Key).ToArray();
                foreach (var n in numbers)
                    numberMask += n.Count() * CardSet.Powers5[(int)n.Key + 1];

                var flushes = common.GroupBy(c => c.Flush).OrderBy(g => g.Key).ToArray();
                foreach (var f in flushes)
                    flushMask += f.Count() * CardSet.Powers6[(int)f.Key + 1];

                if (common.Length < 4)
                    Parallel.For(0, 13, i =>
                    {
                        if (_numbersOnHands[i] + (numberMask % CardSet.Powers5[i]) / CardSet.Powers5[i + 1] < 4)
                            for (int j = 0; j < 4; j++)
                                if (!_used[i, j] && !common.Contains(new Card((FlushType)j, (NumberType)i)))
                                    GetCounts(common.Length, rawIndex, netIndex, (i << 2) + j, i, numberMask + CardSet.Powers5[i + 1], flushMask + CardSet.Powers6[j + 1]);//, -1);
                    });
                else if (common.Length == 4)
                {
                    for (int i = 0; i < 13; i++)
                        if (_numbersOnHands[i] + (numberMask % CardSet.Powers5[i]) / CardSet.Powers5[i + 1] < 4)
                            for (int j = 0; j < 4; j++)
                                if (!_used[i, j] && !common.Contains(new Card((FlushType)j, (NumberType)i)))
                                {
                                    GetCountsInternal(rawIndex + (i << 2) + j, netIndex + i, flushMask + CardSet.Powers6[j + 1]);
                                }
                }
                else
                    GetCountsInternal(rawIndex, netIndex, flushMask);//, 1);
            }
        }
    }
}