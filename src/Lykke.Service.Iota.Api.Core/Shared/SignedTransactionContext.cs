namespace Lykke.Service.Iota.Api.Core.Shared
{
    public class SignedTransactionContext
    {
        public string Hash { get; set; }
        public string[] Transactions { get; set; }
    }
}
