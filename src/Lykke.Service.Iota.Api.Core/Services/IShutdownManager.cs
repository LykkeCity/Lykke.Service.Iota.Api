using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Core.Services
{
    public interface IShutdownManager
    {
        Task StopAsync();
    }
}