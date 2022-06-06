using Binance.Net.Clients;
using Binance.Net.Interfaces.Clients.UsdFuturesApi;
using BinanceAlgorithmLight.Errors;
using CryptoExchange.Net.Authentication;
using System;

namespace BinanceAlgorithmLight.Binance
{
    public class Socket
    {
        public string ApiKey { get; private set; }
        public string SecretKey { get; private set; }
        public BinanceClient client = new BinanceClient();
        public BinanceSocketClient socketClient = new BinanceSocketClient();
        public IBinanceClientUsdFuturesApi futures { get; set; }
        public IBinanceSocketClientUsdFuturesStreams futuresSocket { get; set; }
        public Socket(string api, string secret)
        {
            try
            {
                ApiKey = api;
                SecretKey = secret;
                client.SetApiCredentials(new ApiCredentials(ApiKey, SecretKey));
                socketClient = new BinanceSocketClient();
                socketClient.SetApiCredentials(new ApiCredentials(ApiKey, SecretKey));
                futures = client.UsdFuturesApi;
                futuresSocket = socketClient.UsdFuturesStreams;
            }
            catch (Exception e)
            {
                ErrorText.Add($"Connect {e.Message}");
            }
        }
    }
}
