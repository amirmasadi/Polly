using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Hedging;
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
        /*FallbackAfterRetries();*/
        /*TimeoutStrategy();*/
        Console.ReadKey();
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

    //https://www.pollydocs.org/strategies/fallback#fallback-after-retries
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



    public static void HedgingStrategy()
    {
        // A customized hedging strategy that retries up to 3 times if the execution
        // takes longer than 1 second or if it fails due to an exception or returns an HTTP 500 Internal Server Error.
        var optionsComplex = new HedgingStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(response => response.StatusCode == HttpStatusCode.InternalServerError),
            MaxHedgedAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            ActionGenerator = static args =>
            {
                Console.WriteLine("Preparing to execute hedged action.");

                // Return a delegate function to invoke the original action with the action context.
                // Optionally, you can also create a completely new action to be executed.
                return () => args.Callback(args.ActionContext);
            }
        };
        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>().AddHedging(optionsComplex).Build();
        pipeline.Execute(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    public static void TimeoutStrategy()
    {
        var optionsOnTimeout = new TimeoutStrategyOptions
        {
            TimeoutGenerator = static args =>
            {
                // Note: the timeout generator supports asynchronous operations
                return new ValueTask<TimeSpan>(TimeSpan.FromSeconds(2));
            },
            OnTimeout = static args =>
            {
                Console.WriteLine($"Execution timed out after {args.Timeout.TotalSeconds} seconds.");
                return default;
            }
        };
        var pipeline = new ResiliencePipelineBuilder().AddTimeout(optionsOnTimeout).Build();

        pipeline.ExecuteAsync(static async innerToken => await Task.Delay(TimeSpan.FromSeconds(3), innerToken));
    }
}