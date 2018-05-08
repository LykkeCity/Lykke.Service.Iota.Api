namespace Lykke.Service.Iota.Api.Core.Domain.Address
{
    public interface IAddressVirtual
    {
        string AddressVirtual { get; }
        long LatestAddressIndex { get; }
    }
}
