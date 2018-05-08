using Common.Log;
using Flurl.Http;
using Lykke.Service.Iota.Api.Services.Helpers;
using System;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Services
{
    public class NodeClient
    {
        private readonly ILog _log;
        private readonly string _url;

        public NodeClient(ILog log, string url)
        {
            _log = log;
            _url = url;
        }

        private async Task<T> GetJson<T>(string url, int tryCount = 3)
        {
            bool NeedToRetryException(Exception ex)
            {
                if (ex is FlurlHttpException flurlException)
                {
                    return true;
                }

                return false;
            }

            return await Retry.Try(() => url.GetJsonAsync<T>(), NeedToRetryException, tryCount, _log, 100);
        }
    }
}
