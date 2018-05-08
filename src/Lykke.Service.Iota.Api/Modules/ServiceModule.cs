using Autofac;
using Common.Log;
using Lykke.SettingsReader;
using Lykke.Service.Iota.Api.AzureRepositories.BroadcastInProgress;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Services;
using Lykke.Service.Iota.Api.AzureRepositories.Balance;
using Lykke.Service.Iota.Api.AzureRepositories.BalancePositive;
using Lykke.Service.Iota.Api.AzureRepositories.Broadcast;
using Lykke.Service.Iota.Api.AzureRepositories.Build;
using Lykke.Service.Iota.Api.AzureRepositories.AddressVirtual;
using Lykke.Service.Iota.Api.AzureRepositories.Address;
using Lykke.Service.Iota.Api.Settings;

namespace Lykke.Service.Iota.Api.Modules
{
    public class ServiceModule : Module
    {
        private readonly IReloadingManager<IotaApiSettings> _settings;
        private readonly ILog _log;

        public ServiceModule(IReloadingManager<IotaApiSettings> settings, ILog log)
        {
            _settings = settings;
            _log = log;
        }

        protected override void Load(ContainerBuilder builder)
        {
            var connectionStringManager = _settings.ConnectionString(x => x.Db.DataConnString);

            builder.RegisterInstance(_log)
                .As<ILog>()
                .SingleInstance();

            builder.RegisterType<HealthService>()
                .As<IHealthService>()
                .SingleInstance();

            builder.RegisterType<StartupManager>()
                .As<IStartupManager>();

            builder.RegisterType<ShutdownManager>()
                .As<IShutdownManager>();

            builder.RegisterType<BroadcastRepository>()
                .As<IBroadcastRepository>()
                .WithParameter(TypedParameter.From(connectionStringManager))
                .SingleInstance();

            builder.RegisterType<BroadcastInProgressRepository>()
                .As<IBroadcastInProgressRepository>()
                .WithParameter(TypedParameter.From(connectionStringManager))
                .SingleInstance();

            builder.RegisterType<BalanceRepository>()
                .As<IBalanceRepository>()
                .WithParameter(TypedParameter.From(connectionStringManager))
                .SingleInstance();

            builder.RegisterType<BalancePositiveRepository>()
                .As<IBalancePositiveRepository>()
                .WithParameter(TypedParameter.From(connectionStringManager))
                .SingleInstance();

            builder.RegisterType<BuildRepository>()
                .As<IBuildRepository>()
                .WithParameter(TypedParameter.From(connectionStringManager))
                .SingleInstance();

            builder.RegisterType<AddressRepository>()
                .As<IAddressRepository>()
                .WithParameter(TypedParameter.From(connectionStringManager))
                .SingleInstance();

            builder.RegisterType<AddressVirtualRepository>()
                .As<IAddressVirtualRepository>()
                .WithParameter(TypedParameter.From(connectionStringManager))
                .SingleInstance();            

            builder.RegisterType<IotaService>()
                .As<IIotaService>()
                .WithParameter("network", _settings.CurrentValue.Network)
                .WithParameter("minConfirmations", _settings.CurrentValue.MinConfirmations)
                .SingleInstance();
        }
    }
}
