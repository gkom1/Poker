using PokerService.Cards;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokerService.Cards
{
    public static class VariantCounter
    {
        const int pow13_1 = 13;
        const int pow13_2 = 13 * 13;
        const int pow13_3 = pow13_2 * 13;
        const int pow13_4 = pow13_2 * pow13_2;
        const int pow13_5 = pow13_4 * 13;
        const ushort FLUSH_MASK = ((ushort)CombinationType.FLUSH) << 12;
        const ushort STRAIGHT_FLUSH_MASK = ((ushort)CombinationType.STRAIGHT_FLUSH) << 12;
        const ushort ROYAL_FLUSH_MASK = ((ushort)CombinationType.ROYAL_FLUSH) << 12;

        static FlushType[] flushes;
        static ushort[] comb_costs;

        static VariantCounter()
        {
            flushes = Enumerable.Range(0, 4).Select(x => (FlushType)(byte)x).ToArray();
            var path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), @"..\..\..\PokerService\bin\Debug\PokerTable.dat");
            var bytes = File.ReadAllBytes(path);
            comb_costs = new ushort[bytes.Length / 2];
            for (int i = 0; i < comb_costs.Length; ++i)
                comb_costs[i] = (ushort)(bytes[i * 2] + bytes[i * 2 + 1] * 256);
        }

        public static void LoadTable()
        {

        }

        public static Tuple<long[][], long> CountPreflop(Card[][] players)
        {
            Func<long[][]> make_result = () =>
                Enumerable
                .Range(0, players.Length)
                .Select(x => new long[1 + (int)CombinationType.ROYAL_FLUSH])
                .ToArray();

            var result = make_result();
            long draw = 0;

            var deck = Enumerable
                .Range(0, 13)
                .SelectMany(num => flushes.Select(f => new Card(f, (NumberType)num)))
                .Except(players.SelectMany(p => p))
                .ToArray();

            var pindeces = new int[players.Length];
            for (int i = 0; i < players.Length; ++i)
            {
                if (players[i][0].Number > players[i][1].Number)
                {
                    var p = players[i][0];
                    players[i][0] = players[i][1];
                    players[i][1] = p;
                }
                int f = (int)players[i][0].Number, s = (int)players[i][1].Number;
                pindeces[i] = ((25 - f) * f / 2 + s) * pow13_5;
            }

            var pflushes = players
                .Select(p => flushes.Select(f => p.Count(c => c.Flush == f)).ToArray())
                .ToArray();

            int N = deck.Length;
            int M = N - 4;
            Parallel.For(0, (M + 1) * M / 2
                //, new ParallelOptions { MaxDegreeOfParallelism = 1 }
                , i12 =>
                {
                    ulong[] card_sets = new ulong[players.Length];
                    Card[] card_all = new Card[5];
                    ushort[] costs = new ushort[players.Length];
                    int[] arg_max = new int[players.Length];
                    var possible_flush = new FlushType[players.Length];
                    var always_flush = new bool[players.Length];
                    NumberType[] straight_cards = new NumberType[7];
                    long draw_12 = 0;

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
                    int I2 = pow13_4 * (int)card_all[0].Number + pow13_3 * (int)card_all[1].Number;
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
                        int I3 = I2 + pow13_2 * (int)card_all[2].Number;
                        for (int i = 0; i < f.Length; ++i)
                            ++f[i][(int)card_all[2].Flush];
                        for (int i4 = i3+1; i4 < N-1; ++i4)
                        {
                            card_all[3] = deck[i4];
                            int I4 = I3 + pow13_1 * (int)card_all[3].Number;
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
                                for (int p = 0; p < pindeces.Length; ++p)
                                {
                                    var cost = comb_costs[pindeces[p] + I5];
                                    var comb = (CombinationType)(cost >> 12);
                                    bool isFlush = always_flush[p] || possible_flush[p] == c5.Flush;
                                    if (isFlush)
                                    {
                                        if (comb == CombinationType.STRAIGHT)
                                        {
                                            int straight_cards_count = 0;
                                            Card cp1 = players[p][0], cp2 = players[p][1];
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
                                                if (!isSF && straight_cards[straight_cards_count - 1] == NumberType.A && straight_cards[3] == NumberType._5)
                                                    isSF = true;

                                                if (isSF)
                                                {
                                                    if (straight_cards[straight_cards_count - 1] == NumberType.A
                                                        && straight_cards[straight_cards_count - 5] == NumberType._10)
                                                        cost = (ushort)(ROYAL_FLUSH_MASK | (cost & 0x0FFF));
                                                    else
                                                        cost = (ushort)(STRAIGHT_FLUSH_MASK | (cost & 0x0FFF));
                                                }
                                                else
                                                    cost = FLUSH_MASK;
                                            }
                                            else
                                                cost = FLUSH_MASK;
                                        }
                                        else
                                            cost = FLUSH_MASK;
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
                                if (arg_max_count == 1)
                                {
                                    ++r[arg_max[0]][(int)max_comb];
                                    continue;
                                }
                                switch (max_comb)
                                {
                                    case CombinationType.PAIR:
                                    case CombinationType.NONE:
                                    case CombinationType.FLUSH:
                                        break;
                                    default:
                                        ++draw_12;
                                        continue;
                                }
                                for (int p = 0; p < card_sets.Length; ++p)
                                    card_sets[p] = 0;

                                if (max_comb == CombinationType.PAIR)
                                {
                                    NumberType pair = (NumberType)((max_cost >> 8) & 0xF);
                                    for (int p = 0; p < arg_max_count; ++p )
                                        card_sets[arg_max[p]] = players[arg_max[p]].Concat(card_all)
                                                .Select(c => c.Number)
                                                .Where(x => x != pair)
                                                .OrderByDescending(x => x)
                                                .Take(3)
                                                .Aggregate(0ul, (acc, n) => (acc << 4) + (ulong)n);
                                }
                                else if (max_comb == CombinationType.NONE)
                                {
                                    for (int p = 0; p < arg_max_count; ++p)
                                        card_sets[arg_max[p]] = players[arg_max[p]].Concat(card_all)
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
                                            var pp = players[arg_max[p]];
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
                                            var pp = players[arg_max[p]];
                                            if (pp[1].Flush == flush)
                                                    card_sets[arg_max[p]] = (ulong)pp[1].Number;
                                            else
                                                card_sets[arg_max[p]] = (ulong)pp[0].Number;
                                        }
                                }

                                arg_max_count = 1;
                                var winner_set = card_sets[0];
                                var winner = 0;
                                for (int p = 1; p < card_sets.Length; ++p)
                                    if (card_sets[p] > winner_set)
                                    {
                                        winner = p;
                                        winner_set = card_sets[p];
                                        arg_max_count = 1;
                                    }
                                    else if (card_sets[p] == winner_set)
                                        ++arg_max_count;

                                if (arg_max_count == 1)
                                {
                                    ++r[winner][(int)max_comb];
                                }
                                else
                                {
                                    ++draw_12;
                                }
                            }
                        }
                        for (int i = 0; i < f.Length; ++i)
                            --f[i][(int)card_all[2].Flush];
                    }

                    lock (result)
                    {
                        for (int i = 0; i < players.Length; ++i)
                            for (int j = 0; j <= (int)CombinationType.ROYAL_FLUSH; ++j)
                                result[i][j] += r[i][j];
                        draw += draw_12;
                    }
                });

            return Tuple.Create(result, draw);
        }
    }
}
