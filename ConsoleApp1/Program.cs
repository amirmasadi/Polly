using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimit;
using Polly.Retry;
using Polly.Timeout;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Xml.Serialization;

// sources:
// https://www.zenrows.com/blog/c-sharp-polly-retry#implement-an-exponential-backoff-strategy-with-polly-with-csharp
// https://github.com/App-vNext/Polly
internal class Program
{
    private static void Main(string[] args)
    {
        string url = "https://scrapeme.live/shop/asd";
        PollyRetryWithPipeline(url);
    }

    async public static void PollyRetryWithPipeline(string url)
    {
        using (var httpClient = new HttpClient())
        {
            var optionsComplex = new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(r => r.StatusCode == HttpStatusCode.NotFound),
                MaxRetryAttempts = 4,
                Delay = TimeSpan.Zero,
                OnRetry = static args =>
                {
                    Console.WriteLine("OnRetry, Attempt: {0}", args.AttemptNumber);
                    // Event handlers can be asynchronous; here, we return an empty ValueTask.
                    return default;
                }
            };

            var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(optionsComplex)
            .Build();

            var response = await pipeline.ExecuteAsync(async token => await httpClient.GetAsync(url));
            Console.WriteLine($"Response: {response.StatusCode}");
        }
    }
}