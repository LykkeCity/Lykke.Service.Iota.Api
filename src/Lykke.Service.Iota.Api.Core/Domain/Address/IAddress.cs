namespace Lykke.Service.Iota.Api.Core.Domain.Address
{
    public interface IAddress
    {
        string AddressVirtual { get; }
        string Address { get; }
        long Index { get; }
    }
}
