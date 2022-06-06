using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;
using BinanceAlgorithmLight.Binance;
using BinanceAlgorithmLight.Errors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BinanceAlgorithmLight.Algorithm
{
    public static class Algorithm
    {
        public static long Order(Socket socket, string symbol, OrderSide side, FuturesOrderType type, decimal quantity, PositionSide position_side)
        {
            var result = socket.futures.Trading.PlaceOrderAsync(symbol: symbol, side: side, type: type, quantity: quantity, positionSide: position_side).Result;
            if (!result.Success) ErrorText.Add($"Failed OpenOrder: {result.Error.Message}");
            return result.Data.Id;
        }
        public static PositionSide InfoOrderPositionSide(Socket socket, string symbol, long order_id)
        {
            var result = socket.futures.Trading.GetOrderAsync(symbol: symbol, orderId: order_id).Result;
            if (!result.Success)
            {
                ErrorText.Add($"InfoOrderPositionSide: {result.Error.Message}");
                return InfoOrderPositionSide(socket, symbol, order_id);
            }
            return result.Data.PositionSide;
        }
        public static List<BinanceFuturesOrder> InfoOrder(Socket socket, string symbol, DateTime start_time)
        {
            var result = socket.futures.Trading.GetOrdersAsync(symbol: symbol, startTime: start_time).Result;
            if (!result.Success)
            {
                ErrorText.Add($"InfoOrder: {result.Error.Message}");
                return InfoOrder(socket, symbol, start_time);
            }
            return result.Data.ToList();
        }
        public static decimal InfoOrderId(Socket socket, string symbol, long orderId)
        {
            var result = socket.futures.Trading.GetOrdersAsync(symbol: symbol, orderId: orderId).Result;
            if (!result.Success)
            {
                ErrorText.Add($"InfoOrderId: {result.Error.Message}");
                return InfoOrderId(socket, symbol, orderId);
            }
            else
            {
                foreach (var it in result.Data.ToList())
                {
                    if (it.AvgPrice > 0m) return it.AvgPrice;
                }
                return InfoOrderId(socket, symbol, orderId);
            }
        }
    }
}
