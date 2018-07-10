namespace Lykke.Service.Iota.Api.Shared
{
    public class TransactionContext
    {
        public TransactionType Type { get; set; }
        public TransactionInput[] Inputs { get; set; }
        public TransactionOutput[] Outputs { get; set; }
    }
}
