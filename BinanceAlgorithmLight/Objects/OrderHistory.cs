using Binance.Net.Objects.Models.Futures.Socket;
using Binance.Net.Enums;

namespace BinanceAlgorithmLight.Objects
{
    public class OrderHistory : BinanceFuturesStreamOrderUpdateData
    {
        public decimal Total { get; set; }
        public string ColorTotal { get; set; }
        public string ColorRealizedProfit { get; set; }
        public string ColorPositionSide { get; set; }
        public string Trade { get; set; }
        public string ColorTrade { get; set; }
        public OrderHistory(BinanceFuturesStreamOrderUpdateData order)
        {
            UpdateTime = order.UpdateTime;
            Symbol = order.Symbol;
            AveragePrice = order.AveragePrice;
            QuantityOfLastFilledTrade = order.QuantityOfLastFilledTrade;
            RealizedProfit = order.RealizedProfit;
            Fee = order.Fee;
            Side = order.Side;
            PositionSide = order.PositionSide;
            Total = RealizedProfit - Fee;
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
