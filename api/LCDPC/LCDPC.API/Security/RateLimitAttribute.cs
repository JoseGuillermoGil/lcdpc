using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LCDPC.API.Security;

/// <summary>
/// Action filter that rate-limits requests per IP address.
/// Uses a sliding window of fixed time buckets.
/// Returns 429 Too Many Requests when the limit is exceeded.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RateLimitAttribute(int maxRequests, int windowSeconds) : ActionFilterAttribute
{
    // Global rate limit store: key = IP address, value = list of timestamps
    private static readonly ConcurrentDictionary<string, List<DateTime>> RequestLog = new();

    private readonly int _maxRequests = maxRequests;
    private readonly int _windowSeconds = windowSeconds;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;
        var windowStart = now.AddSeconds(-_windowSeconds);

        var timestamps = RequestLog.GetOrAdd(ip, _ => []);

        // Remove expired entries
        lock (timestamps)
        {
            timestamps.RemoveAll(t => t < windowStart);

            if (timestamps.Count >= _maxRequests)
            {
                // Calculate retry-after based on oldest request in the window
                var oldestInWindow = timestamps[0];
                var retryAfterSeconds = Math.Ceiling((oldestInWindow.AddSeconds(_windowSeconds) - now).TotalSeconds);
                retryAfterSeconds = Math.Max(1, retryAfterSeconds);

                context.HttpContext.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
                context.Result = new ObjectResult(new
                {
                    error = "rate_limit_exceeded",
                    error_description = $"Too many requests. Maximum {_maxRequests} requests per {_windowSeconds} seconds.",
                    retry_after = (int)retryAfterSeconds
                })
                {
                    StatusCode = StatusCodes.Status429TooManyRequests
                };
                return;
            }

            timestamps.Add(now);
        }

        base.OnActionExecuting(context);
    }

    /// <summary>
    /// Clears the rate limit store (useful for testing).
    /// </summary>
    public static void Clear() => RequestLog.Clear();
}
