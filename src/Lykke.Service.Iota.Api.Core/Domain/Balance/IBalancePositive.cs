namespace Lykke.Service.Iota.Api.Core.Domain.Balance
{
    public interface IBalancePositive
    {
        string Address { get; }
        decimal Amount { get; }
        long Block { get; }
    }
}
