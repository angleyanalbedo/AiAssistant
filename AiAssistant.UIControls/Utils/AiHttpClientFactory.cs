using System.Net;
using System.Net.Http;

namespace AiAssistant.UIControls.Utils
{
    internal static class AiHttpClientFactory
    {
        public static HttpClient Create()
        {
            EnsureLegacyTlsSupport();
            return new HttpClient();
        }

        private static void EnsureLegacyTlsSupport()
        {
#if NET45
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
#endif
        }
    }
}
