// This is mostly lifted straight from here: https://github.com/omansak/libvideo/blob/master/src/libvideo.debug/CustomYoutubeClient.cs
using System.Net;
using System.Net.Http.Headers;

namespace VideoLibrary.Debug
{
    class CustomHandler
    {
        public HttpMessageHandler GetHandler()
        {
            CookieContainer cookieContainer = new();
            cookieContainer.Add(new Cookie("CONSENT", "YES+cb", "/", "youtube.com"));
            return new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = cookieContainer
            };

        }
    }
    class CustomYouTube : YouTube
    {
        private const long CHUNK_SIZE = 10_485_760;
        private HttpClient Http = new();
        protected override HttpClient MakeClient(HttpMessageHandler handler)
        {
            return base.MakeClient(handler);
        }
        protected override HttpMessageHandler MakeHandler()
        {
            return new CustomHandler().GetHandler();
        }
        public async Task DownloadAsync(Uri uri, string filePath, IProgress<Tuple<long, long>> progress)
        {
            var totalBytesCopied = 0L;
            var file_size = await GetContentLengthAsync(uri.AbsoluteUri) ?? 0;
            if (file_size == 0)
                throw new Exception("File has no any content!");
            using Stream output = File.OpenWrite(filePath);

            var segmentCount = (int)Math.Ceiling(1.0 * file_size / CHUNK_SIZE);
            for (var i = 0; i < segmentCount; i++)
            {
                var from = i * CHUNK_SIZE;
                var to = (i + 1) * CHUNK_SIZE - 1;
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Range = new RangeHeaderValue(from, to);
                using (request)
                {
                    // Download Stream
                    var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode)
                        response.EnsureSuccessStatusCode();
                    var stream = await response.Content.ReadAsStreamAsync();
                    //File Steam
                    var buffer = new byte[81920];
                    int bytesCopied;
                    do
                    {
                        bytesCopied = await stream.ReadAsync(buffer);
                        output.Write(buffer, 0, bytesCopied);
                        totalBytesCopied += bytesCopied;
                        progress.Report(new Tuple<long, long>(totalBytesCopied, file_size));
                    } while (bytesCopied > 0);
                }
            }
        }
        private async Task<long?> GetContentLengthAsync(string requestUri, bool ensureSuccess = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, requestUri);
            var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (ensureSuccess)
                response.EnsureSuccessStatusCode();
            return response.Content.Headers.ContentLength;
        }
    }
}