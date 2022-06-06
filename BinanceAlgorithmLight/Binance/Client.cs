namespace BinanceAlgorithmLight.Binance
{
    public class Client
    {
        public string ClientName { get; set; }
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
        public Client(string ClientName, string ApiKey, string SecretKey)
        {
            this.ClientName = ClientName;
            this.ApiKey = ApiKey;
            this.SecretKey = SecretKey;
        }
    }
}
