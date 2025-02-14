using DLNAServer.Features.ApiBlocking.Interfaces;

namespace DLNAServer.Features.ApiBlocking
{
    public class ApiBlockerService : IApiBlockerService
    {
        public bool IsBlocked { get; private set; }
        public string Reason { get; private set; } = string.Empty;
        public void BlockApi(bool block, string reason)
        {
            IsBlocked = block;
            Reason = reason;
        }
    }
}
