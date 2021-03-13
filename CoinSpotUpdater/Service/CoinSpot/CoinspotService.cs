using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Configuration;

using Newtonsoft.Json;
using CoinSpotUpdater.CoinSpot.Dto;
using System.Diagnostics;

namespace CoinSpotUpdater.CoinSpot
{
    // see https://www.coinspot.com.au/api for full api
    class CoinspotService
    {
        private readonly string _key;
        private readonly string _secret;
        private readonly string _baseUrl;
        private const string _baseReadOnlyUrl = "/api/ro/my/";
        private const string _baseWriteUrl = "/api/";
        private Stopwatch _stopWatch;

        public CoinspotService()
        {
            _key = FromAppSettings("coinSpotKey");
            _secret = FromAppSettings("coinSpotSecret");
            _baseUrl = FromAppSettings("coinSpotSite");
            _stopWatch = new Stopwatch();
        }

        public static string FromAppSettings(string key)
            => ConfigurationManager.AppSettings.Get(key);

        public string QuickSell(string coin, float aud)
            => PrivateApiCallJson(_baseWriteUrl + "quote/sell", JsonConvert.SerializeObject(new CoinSpotQuickSellOrder() { amount = aud, cointype = coin }));

        public string Sell(string coin, float aud, float rate)
            => PrivateApiCallJson(_baseWriteUrl + "sell", JsonConvert.SerializeObject(new CoinSpotSellOrder() { amount = aud, cointype = coin, rate = rate }));

        public string Buy(string coin, float aud, float rate)
            => PrivateApiCallJson(_baseWriteUrl + "sell", JsonConvert.SerializeObject(new CoinSpotBuyOrder() { amount = aud, cointype = coin }));

        public float GetPortfolioValue()
            => GetMyBalances().GetTotal();

        public CoinSpotBalances GetMyBalances()
            => JsonConvert.DeserializeObject<CoinSpotBalances>(GetMyBalancesJson));

        public string GetMyBalancesJson(string JSONParameters = "{}")
            => PrivateApiCallJson(_baseReadOnlyUrl + "balances", JSONParameters);

        public string GetCoinBalanceJson(string coinType)
            => PrivateApiCallJson(_baseReadOnlyUrl + "balances/:" + coinType);

        internal CoinSpotAllPrices GetAllPrices()
            => JsonConvert.DeserializeObject<CoinSpotAllPrices>(PublicApiCall("/pubapi/latest"));

        internal CoinSpotTransactions GetAllTransactions()
            => JsonConvert.DeserializeObject<CoinSpotTransactions>(PrivateApiCallJson(_baseReadOnlyUrl + "transactions/open"));

        internal CoinSpotDeposits GetAllDeposits()
            => JsonConvert.DeserializeObject<CoinSpotDeposits>(PrivateApiCallJson(_baseReadOnlyUrl + "deposits"));

        public string PrivateApiCall(string endPoint)
            => PrivateApiCall(endPoint, "{}");

        public string PublicApiCall(string url)
        {
            var call = _baseUrl + url;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(call);
            using (var reader = new StreamReader(request.GetResponse().GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }

        public string PrivateApiCall(string endPoint, string jsonParameters)
        {
            var endpointURL = _baseUrl + endPoint;
            long nonce = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
            var json = jsonParameters.Replace(" ", "");
            var nonceParameter = "\"nonce\"" + ":" + nonce;
            if (json != "{}")
            {
                nonceParameter += ",";
            }

            var parameters = jsonParameters.Trim().Insert(1, nonceParameter);
            var parameterBytes = Encoding.UTF8.GetBytes(parameters);
            var signedData = SignData(parameterBytes);
            var request = MakeRequest(endpointURL, parameterBytes, signedData);

            return MakeCall(parameterBytes, request);
        }

        private string PrivateApiCallJson(string endPointUrl, string JSONParameters = "{}")
            => PrivateApiCall(endPointUrl, JSONParameters);

        private HttpWebRequest MakeRequest(string endpointURL, byte[] parameterBytes, string signedData)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(endpointURL);
            request.KeepAlive = false;
            request.Method = "POST";
            request.Headers.Add("key", _key);
            request.Headers.Add("sign", signedData.ToLower());
            request.ContentType = "application/json";
            request.ContentLength = parameterBytes.Length;
            return request;
        }

        private string MakeCall(byte[] parameterBytes, HttpWebRequest request)
        {
            WaitForCoinSpotApi();

            string responseText;
            try
            {
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(parameterBytes, 0, parameterBytes.Length);
                    stream.Close();
                }
                using (var reader = new StreamReader(request.GetResponse().GetResponseStream()))
                {
                    responseText = reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                responseText = ex.Message;
            }

            return responseText;
        }

        private string SignData(byte[] JSONData)
        {
            var encodedBytes = new HMACSHA512(Encoding.UTF8.GetBytes(_secret)).ComputeHash(JSONData);
            var sb = new StringBuilder();
            for (int i = 0; i <= encodedBytes.Length - 1; i++)
            {
                sb.Append(encodedBytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        internal string Buy(string v1, float v2)
        {
            throw new NotImplementedException("Cannot buy, unsure why");
        }

        private void WaitForCoinSpotApi()
        {
            if (_stopWatch.ElapsedMilliseconds < 1000)
            {
                System.Threading.Thread.Sleep((int)(1000L - _stopWatch.ElapsedMilliseconds + 10));
            }
            _stopWatch.Reset();
        }
    }
}
