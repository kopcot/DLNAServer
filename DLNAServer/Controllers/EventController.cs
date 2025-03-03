using DLNAServer.Features.Subscriptions.Data;
using DLNAServer.Features.Subscriptions.Interfaces;
using DLNAServer.Helpers.Attributes;
using DLNAServer.Helpers.Logger;
using Microsoft.AspNetCore.Mvc;

namespace DLNAServer.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public partial class EventController : Controller
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
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(SubscribeAction),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);

            var callback = Request.Headers.FirstOrDefault(h => h.Key.Contains("Callback", StringComparison.InvariantCultureIgnoreCase)).Value.FirstOrDefault();
            var timeout = Request.Headers.FirstOrDefault(h => h.Key.Contains("Timeout", StringComparison.InvariantCultureIgnoreCase)).Value.FirstOrDefault();
            var sid = Request.Headers.FirstOrDefault(h => h.Key.Contains("SID", StringComparison.InvariantCultureIgnoreCase)).Value.FirstOrDefault();
            var timeoutNumber = int.TryParse(new string(timeout?.Where(c => char.IsDigit(c)).ToArray()), out int number) ? number : -9999;

            if (string.IsNullOrWhiteSpace(callback)
                || string.IsNullOrWhiteSpace(timeout)
                || timeoutNumber == -9999)
            {
                WarningIncorrectRequestParameter(callback, timeout, timeoutNumber);
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

            await Task.CompletedTask;
            return Ok("subscribed");
        }
        [HttpUnsubscribe("eventAction/{serviceID}")]
        public async Task<IActionResult> UnsubscribeAction([FromRoute] string serviceID)
        {
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(UnsubscribeAction),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);

            await Task.CompletedTask;
            return Ok("unsubscribed");
        }
    }
}
