using Newtonsoft.Json;
using PokerService.Cards;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Http;
using System.Xml;
using System.Xml.Serialization;

namespace PokerService
{
    public class ServiceController : ApiController
    {
        private const int COMBINATIONS_COUNT = 10;
        private const int MAX_COEFFICIENT = 0;
        private static TablesInfo _tables;
        private static System.Globalization.NumberFormatInfo nfi = System.Globalization.CultureInfo.InvariantCulture.NumberFormat;

        public class CombinationInfo
        {
            [XmlAttribute("type")]
            public string CombinationType { get; set; }
            
            [XmlAttribute("value")]
            public double Value { get; set; }
        }

        public class TableInfo
        {
            [XmlAttribute("count")]
            public int Count { get; set; }

            [XmlAttribute("player")]
            public double Player { get; set; }

            [XmlArray("any")]
            [XmlArrayItem("combination")]
            public CombinationInfo[] Combinations { get; set; }

            [XmlArray("express")]
            [XmlArrayItem("combination")]
            public CombinationInfo[] ExpressCombinations { get; set; }
        }

        [XmlRoot("tablesRoot")]
        public class TablesInfo
        {
            [XmlArray("tables")]
            [XmlArrayItem("table", typeof(TableInfo))]
            public TableInfo[] Tables { get; set; }
        }

        public static void Init()
        {
            var ser = new XmlSerializer(typeof(TablesInfo));
            using (var tr = new StreamReader(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "AprioriCoeffs.xml")))
            {
                try
                {
                    _tables = (TablesInfo)ser.Deserialize(tr);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString() + "\r\n(error in reading XML)");
                }
            }
        }

        const double MarginMultiplier = 1;
        public Dictionary<string, object> Post([FromBody]Dictionary<string, TableJson[]> inputJson, bool exact = true)
        {
            string playerKey;
            Stopwatch sw = new Stopwatch();
            double[] playersCount, combinationsCount;
            TableJson[] args = inputJson["tables"];
            Table[] tables = new Table[args.Length];
            CountJson[] resultJson = new CountJson[args.Length];
            ComboCheckJson[] comboCheckJson = new ComboCheckJson[args.Length];

            Func<double, double, string> getCoefficient = (x, overallCount) => (x == 0 ? MAX_COEFFICIENT : Math.Round(overallCount / x * MarginMultiplier, 4)).ToString(nfi);
            Func<int, int, ulong> C = (pc, cc) => { ulong num = 1; for (int k = 1; k <= 5 - cc; k++) { num = num * (ulong)(53 - 2 * pc - k - cc) / (ulong)k; } return num; };

            sw.Start();
            for (int i = 0; i < args.Length; i++)
            {
                resultJson[i] = new CountJson();

                if (args[i].Players.Length == 0)
                    return new Dictionary<string, object>() { { "timeElapsed", 0 }, { "result", "incorrect request" } };

                if (args[i].Players[0].Length == 0) // чтение из XML
                {
                    try
                    {
                        TableInfo rt = _tables.Tables.First(ti => ti.Count == args[i].Players.Length);
                        for (int j = 0; j < args[i].Players.Length; j++)
                        {
                            resultJson[i].ExpressCoefficients.Add(string.Format("p{0}{1}", i, j), new Dictionary<string, string>());
                            resultJson[i].PlayersCoefficients.Add(string.Format("p{0}{1}", i, j), Math.Round(rt.Player * MarginMultiplier, 4).ToString(nfi));
                        }
                        foreach (CombinationInfo ct in rt.Combinations)
                            resultJson[i].CombinationCoefficients.Add(ct.CombinationType, Math.Round(ct.Value * MarginMultiplier, 4).ToString(nfi));
                        foreach (CombinationInfo ct in rt.ExpressCombinations)
                            for (int j = 0; j < args[i].Players.Length; j++)
                                resultJson[i].ExpressCoefficients[string.Format("p{0}{1}", i, j)].Add(ct.CombinationType, Math.Round(ct.Value * MarginMultiplier, 4).ToString(nfi));
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex);
                        throw;
                    }
                }
                else if(args[i].Common.Length == CardSet.COMMON_CARDS_COUNT) // проверка комбинаций
                {
                    Card[] common = args[i].Common.ToCards();
                    Card[][] playersCards = args[i].Players.ToCards();
                    CardSet[] sets = playersCards.Select(p => new CardSet(p.OrderBy(c => c.Number).Union(common).ToArray(), true)).ToArray();
                    Dictionary<int, Card[]> combinations;
                    CombinationType type = CardSet.GetFinalCombinations(sets, out combinations);
                    comboCheckJson[i] = new ComboCheckJson
                    {
                        Combination = type.ToString(),
                        PlayersCombinations = combinations.ToDictionary(kvp => string.Format("p{0}{1}", i, kvp.Key), kvp => kvp.Value.Select(c => common.Contains(c) ? Array.IndexOf(common, c) : Array.IndexOf(playersCards[kvp.Key], c) + 5).OrderBy(k => k).ToArray())
                    };
                }
                else // расчет
                {
                    try
                    {
                        tables[i] = new Table(args[i].Players.ToCards(), exact);
                        tables[i].GetProbabilities(args[i].Common.ToCards());
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(System.DateTime.UtcNow);
                        Console.Error.WriteLine(JsonConvert.SerializeObject(inputJson));
                        Console.Error.WriteLine(ex.ToString() + "\r\n(error in calculating counts)");
                        throw;
                    }
                    
                    double overallCount = C(args[i].Players.Length, args[i].Common.Length); //(double)tables[i].Counts.Select(p => p.Sum()).Sum();
                    playersCount = tables[i].Counts.Select(p => p.Sum()).Take(args[i].Players.Length).ToArray();
                    combinationsCount = tables[i].Counts[args[i].Players.Length];

                    try
                    {
                        for (int j = 0; j < args[i].Players.Length; j++)
                        {
                            playerKey = string.Format("p{0}{1}", i, j);

                            resultJson[i].PlayersCoefficients.Add(playerKey, getCoefficient(playersCount[j], overallCount));
                            for (int k = 0; k < COMBINATIONS_COUNT; k++)
                            {
                                double value = tables[i].Counts[j][k];

                                if (!resultJson[i].ExpressCoefficients.ContainsKey(playerKey))
                                    resultJson[i].ExpressCoefficients.Add(playerKey, new Dictionary<string, string>());
                                resultJson[i].ExpressCoefficients[playerKey].Add(((CombinationType)k).ToString(), getCoefficient(value, overallCount));
                            }
                        }

                        for (int k = 0; k < COMBINATIONS_COUNT; k++)
                            resultJson[i].CombinationCoefficients.Add(((CombinationType)k).ToString(), getCoefficient(combinationsCount[k], overallCount));
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(System.DateTime.UtcNow);
                        Console.Error.WriteLine(JsonConvert.SerializeObject(inputJson));
                        Console.Error.WriteLine(ex.ToString() + "\r\n(error in writing coefficients)");
                        throw;
                    }
                }
            }
            int te = (int)sw.ElapsedMilliseconds;
            sw.Stop();

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var mode = exact ? "exact" : "clone";
            return new Dictionary<string, object>() { { "timeElapsed", te }, {"version", version}, {"mode", mode}, { "coeffs", resultJson }, {"results", comboCheckJson} };
        }
    }
}