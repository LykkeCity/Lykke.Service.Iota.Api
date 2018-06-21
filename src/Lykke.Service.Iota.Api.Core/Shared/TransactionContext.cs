namespace Lykke.Service.Iota.Api.Core.Shared
{
    public class TransactionContext
    {
        public TransactionType Type { get; set; }
        public TransactionInput[] Inputs { get; set; }
        public TransactionOutput[] Outputs { get; set; }
    }
}
