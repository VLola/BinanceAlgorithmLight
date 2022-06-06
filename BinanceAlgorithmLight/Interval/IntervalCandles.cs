using Binance.Net.Enums;
using System.Collections.Generic;

namespace BinanceAlgorithmLight.Interval
{
    public static class IntervalCandles
    {
        public static List<Interval> Intervals()
        {
            List<Interval> Intervals = new List<Interval>();
            Intervals.Add(new Interval() { name = "One Minute", interval = KlineInterval.OneMinute, timespan = 600000000 });
            Intervals.Add(new Interval() { name = "Three Minutes", interval = KlineInterval.ThreeMinutes, timespan = 1800000000 });
            Intervals.Add(new Interval() { name = "Five Minutes", interval = KlineInterval.FiveMinutes, timespan = 3000000000 });
            Intervals.Add(new Interval() { name = "Fifteen Minutes", interval = KlineInterval.FifteenMinutes, timespan = 9000000000 });
            Intervals.Add(new Interval() { name = "Thirty Minutes", interval = KlineInterval.ThirtyMinutes, timespan = 18000000000 });
            Intervals.Add(new Interval() { name = "One Hour", interval = KlineInterval.OneHour, timespan = 36000000000 });
            Intervals.Add(new Interval() { name = "Two Hour", interval = KlineInterval.TwoHour, timespan = 72000000000 });
            Intervals.Add(new Interval() { name = "Four Hour", interval = KlineInterval.FourHour, timespan = 144000000000 });
            Intervals.Add(new Interval() { name = "Six Hour", interval = KlineInterval.SixHour, timespan = 216000000000 });
            Intervals.Add(new Interval() { name = "Eight Hour", interval = KlineInterval.EightHour, timespan = 288000000000 });
            Intervals.Add(new Interval() { name = "Twelve Hour", interval = KlineInterval.TwelveHour, timespan = 432000000000 });
            Intervals.Add(new Interval() { name = "One Day", interval = KlineInterval.OneDay, timespan = 864000000000 });
            Intervals.Add(new Interval() { name = "Three Day", interval = KlineInterval.ThreeDay, timespan = 2592000000000 });
            Intervals.Add(new Interval() { name = "One Week", interval = KlineInterval.OneWeek, timespan = 6048000000000 });
            Intervals.Add(new Interval() { name = "One Month", interval = KlineInterval.OneMonth, timespan = 25920000000000 });
            return Intervals;
        }
    }
    public class Interval
    {
        public string name { get; set; }
        public KlineInterval interval { get; set; }
        public long timespan { get; set; }
        public override string ToString()
        {
            return name;
        }
    }
}
