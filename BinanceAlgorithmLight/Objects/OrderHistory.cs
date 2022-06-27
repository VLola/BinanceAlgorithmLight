using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;
using System;

namespace BinanceAlgorithmLight.Objects
{
    public class OrderHistory: BinanceFuturesOrder
    {
        public decimal Total { get; set; }
        public decimal RealizedProfit { get; set; }
        public decimal Commission { get; set; }
        public string ColorTotal { get; set; }
        public string ColorRealizedProfit { get; set; }
        public string ColorPositionSide { get; set; }
        public string Trade { get; set; }
        public string ColorTrade { get; set; }
        public OrderHistory(BinanceFuturesOrder order, decimal SLPrice)
        {
            Id = order.Id;
            UpdateTime = order.UpdateTime;
            Symbol = order.Symbol;
            AvgPrice = order.AvgPrice;
            Quantity = order.Quantity;
            PositionSide = order.PositionSide;
            Side = order.Side;
            if (SLPrice == 0m) RealizedProfit = 0m;
            else
            {
                decimal rp = ((AvgPrice * Quantity) - (SLPrice * Quantity));
                if (PositionSide == PositionSide.Long) RealizedProfit = rp;
                else if (PositionSide == PositionSide.Short) RealizedProfit = -rp;
            }
            Commission = ((AvgPrice * Quantity) * 0.0004m);
             Total = RealizedProfit - Commission;
            // Colot Total
            if (Total > 0m) ColorTotal = "Green";
            else if (Total < 0m) ColorTotal = "Red";
            else ColorTotal = "White";
            // Color RealizedProfit
            if (RealizedProfit > 0m) ColorRealizedProfit = "Green";
            else if (RealizedProfit < 0m) ColorRealizedProfit = "Red";
            else ColorRealizedProfit = "White";
            // Color PositionSide
            if (PositionSide == PositionSide.Long) ColorPositionSide = "Green";
            else if (PositionSide == PositionSide.Short) ColorPositionSide = "Red";
            else ColorPositionSide = "White";
            // Trade
            if (PositionSide == PositionSide.Long && Side == OrderSide.Buy || PositionSide == PositionSide.Short && Side == OrderSide.Sell) { Trade = "Open Trade"; ColorTrade = "LightBlue"; }
            else if (PositionSide == PositionSide.Long && Side == OrderSide.Sell || PositionSide == PositionSide.Short && Side == OrderSide.Buy) { Trade = "Close Trade"; ColorTrade = "Orange"; }
            else { Trade = "Both"; ColorTrade = "White"; }
        }
        public OrderHistory() { }
    }
}
