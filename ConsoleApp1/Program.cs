using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.RateLimit;
using Polly.Retry;
using Polly.Timeout;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Xml.Serialization;

// sources:
// https://github.com/App-vNext/Polly
// https://www.zenrows.com/blog/c-sharp-polly-retry#implement-an-exponential-backoff-strategy-with-polly-with-csharp
internal class Program
{
    private static void Main(string[] args)
    {
        string url = "https://scrapeme.live/shop/asd";
        /*RetryStrategy(url);*/
        FallbackAfterRetries();
    }

    async public static void RetryStrategy(string url)
    {
        using (var httpClient = new HttpClient())
        {
            var optionsComplex = new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(r => r.StatusCode == HttpStatusCode.InternalServerError),
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

            /*var response = await pipeline.ExecuteAsync(async token => await httpClient.GetAsync(url));
            Console.WriteLine($"Response: {response.StatusCode}");*/
            pipeline.Execute(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }
    public static void FallbackAfterRetries()
    {
        // Define a common predicates re-used by both fallback and retries
        var predicateBuilder = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r => r.StatusCode == HttpStatusCode.InternalServerError);

        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddFallback(new()
            {
                ShouldHandle = predicateBuilder,
                FallbackAction = args =>
                {
                    // Try to resolve the fallback response
                    HttpResponseMessage fallbackResponse = ResolveFallbackResponse(args.Outcome);
                    return Outcome.FromResultAsValueTask(fallbackResponse);
                },
                OnFallback = static args =>
                {
                    // Add extra logic to be executed when the fallback is triggered, such as logging.
                    Console.WriteLine("OnFallback, here...!");
                    return default; // Returns an empty ValueTask
                }
            })
            .AddRetry(new()
            {
                ShouldHandle = predicateBuilder,
                MaxRetryAttempts = 3,
                OnRetry = static args =>
                {
                    Console.WriteLine("OnRetry, Attempt: {0}", args.AttemptNumber);
                    // Event handlers can be asynchronous; here, we return an empty ValueTask.
                    return default;
                }
            })
            .Build();

        // Demonstrative execution that always produces invalid result
        pipeline.Execute(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

    }

    private static HttpResponseMessage ResolveFallbackResponse(Outcome<HttpResponseMessage> outcome)
    {
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}