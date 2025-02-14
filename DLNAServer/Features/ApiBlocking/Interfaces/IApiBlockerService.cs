namespace DLNAServer.Features.ApiBlocking.Interfaces
{
    public interface IApiBlockerService
    {
        bool IsBlocked { get; }
        string Reason { get; }
        void BlockApi(bool block, string reason = "");
    }
}
