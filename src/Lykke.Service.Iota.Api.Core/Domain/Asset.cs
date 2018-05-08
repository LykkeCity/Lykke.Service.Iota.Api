namespace Lykke.Service.Iota.Api.Core.Domain
{
    public class Asset
    {
        public Asset(string id, int accuracy) => (Id, Accuracy) = (id, accuracy);

        public string Id { get; }
        public int Accuracy { get; }

        public static Asset Miota { get; } = new Asset("MIOTA", 6);
    }
}
