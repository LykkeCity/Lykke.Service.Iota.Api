namespace Lykke.Service.Iota.Api.Core.Domain.Address
{
    public interface IAddressInput
    {
        string AddressVirtual { get; }
        string Address { get; }
        int Index { get; }
    }
}
