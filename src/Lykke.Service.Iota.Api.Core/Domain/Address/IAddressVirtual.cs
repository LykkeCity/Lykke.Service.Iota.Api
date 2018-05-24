namespace Lykke.Service.Iota.Api.Core.Domain.Address
{
    public interface IAddressVirtual
    {
        string AddressVirtual { get; }
        string LatestAddress { get; set; }
        long LatestAddressIndex { get; }
    }
}
