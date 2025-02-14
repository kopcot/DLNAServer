using DLNAServer.Helpers.Interfaces;

namespace DLNAServer.Types.UPNP.Interfaces
{
    public interface IUPNPDevices : IInitializeAble, ITerminateAble
    {
        UPNPDevice[] AllUPNPDevices { get; }
    }
}
