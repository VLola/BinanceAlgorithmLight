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
            var result = socket.futures.Trading.PlaceOrderAsync(symbol: symbol, side: side, type: type, quantity: quantity, positionSide: position_side);
            return result.Result.Data.Id;
        }
    }
}
