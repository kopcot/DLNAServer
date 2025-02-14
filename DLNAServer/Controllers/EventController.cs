using DLNAServer.Features.Subscriptions.Data;
using DLNAServer.Features.Subscriptions.Interfaces;
using DLNAServer.Helpers.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace DLNAServer.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class EventController : Controller
    {
        private readonly ILogger<EventController> _logger;
        private readonly Lazy<ISubscriptionService> _subscriptionServiceLazy;
        private ISubscriptionService SubscriptionService => _subscriptionServiceLazy.Value;
        public EventController(
            ILogger<EventController> logger,
            Lazy<ISubscriptionService> subscriptionServiceLazy)
        {
            _logger = logger;
            _subscriptionServiceLazy = subscriptionServiceLazy;
        }
        [HttpSubscribe("eventAction/{serviceID}")]
        public async Task<IActionResult> SubscribeAction([FromRoute] string serviceID)
        {
            _logger.LogDebug($"{nameof(SubscribeAction)}, {DateTime.Now} - {this.HttpContext.Connection.RemoteIpAddress}:{this.HttpContext.Connection.RemotePort}  path: '{this.ControllerContext.HttpContext.Request.Path.Value}',  method: '{this.HttpContext.Request.Method}', id. '{serviceID}'");

            var callback = Request.Headers.FirstOrDefault(h => h.Key.Contains("Callback", StringComparison.InvariantCultureIgnoreCase)).Value.FirstOrDefault();
            var timeout = Request.Headers.FirstOrDefault(h => h.Key.Contains("Timeout", StringComparison.InvariantCultureIgnoreCase)).Value.FirstOrDefault();
            var sid = Request.Headers.FirstOrDefault(h => h.Key.Contains("SID", StringComparison.InvariantCultureIgnoreCase)).Value.FirstOrDefault();
            var timeoutNumber = int.TryParse(new string(timeout?.Where(c => char.IsDigit(c)).ToArray()), out int number) ? number : -9999;

            if (string.IsNullOrWhiteSpace(callback)
                || string.IsNullOrWhiteSpace(timeout)
                || timeoutNumber == -9999)
            {
                _logger.LogWarning($"Incorrect subscibe request parameters. Callback = '{callback}', Timeout = '{timeout}', TimeoutNumber = {timeoutNumber}");
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(sid)
                || SubscriptionService.GetSubscription(sid!) is not Subscription subscription)
            {
                sid = $"uuid:{Guid.NewGuid()}";
                subscription = SubscriptionService.GetOrAddSubscription(sid!, callback!, TimeSpan.FromSeconds(timeoutNumber));
            }
            else if (subscription.IsExpired())
            {
                SubscriptionService.TryRemoveSubscription(subscription.SID);
                sid = $"uuid:{Guid.NewGuid()}";
                subscription = SubscriptionService.GetOrAddSubscription(sid!, callback!, TimeSpan.FromSeconds(timeoutNumber));
            }

            _ = SubscriptionService.UpdateLastNotifyTime(sid!);

            _ = Response.Headers.TryAdd("SID", subscription.SID);
            _ = Response.Headers.TryAdd("TIMEOUT", subscription.Timeout.ToString());

            LogHeaders(nameof(SubscribeAction), "Request", Request.Headers);
            LogHeaders(nameof(SubscribeAction), "Response", Response.Headers);

            await Task.CompletedTask;
            return Ok("subscribed");
        }
        [HttpUnsubscribe("eventAction/{serviceID}")]
        public async Task<IActionResult> UnsubscribeAction([FromRoute] string serviceID)
        {
            _logger.LogDebug($"{nameof(UnsubscribeAction)}, {DateTime.Now} - {this.HttpContext.Connection.RemoteIpAddress}:{this.HttpContext.Connection.RemotePort}  path: '{this.ControllerContext.HttpContext.Request.Path.Value}',  method: '{this.HttpContext.Request.Method}', id. '{serviceID}'");

            var headersRequest = Request.Headers.Select(x => $"{x.Key}: {x.Value}").ToList();
            _logger.LogInformation($"{nameof(SubscribeAction)} Request Headers: {Environment.NewLine}{string.Join(Environment.NewLine, headersRequest)}");
            var headersResponse = Request.Headers.Select(x => $"{x.Key}: {x.Value}").ToList();
            _logger.LogInformation($"{nameof(SubscribeAction)} Response Headers: {Environment.NewLine}{string.Join(Environment.NewLine, headersResponse)}");

            await Task.CompletedTask;
            return Ok("unsubscribed");
        }
        private void LogHeaders(string method, string type, IHeaderDictionary headers)
        {
            var headersRequest = headers.Select(x => $"{x.Key}: {x.Value}").ToList();
            _logger.LogInformation($"{method}{Environment.NewLine}{type} Headers: {Environment.NewLine}{string.Join(Environment.NewLine, headersRequest)}");
        }
    }
}
