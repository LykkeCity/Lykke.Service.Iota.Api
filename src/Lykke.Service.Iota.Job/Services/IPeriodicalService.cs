using System.Threading.Tasks;

namespace Lykke.Service.Iota.Job.Services
{
    public interface IPeriodicalService
    {
        Task UpdateBalances();
        Task UpdateBroadcasts();
        Task PromoteBroadcasts();
    }
}
