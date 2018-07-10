using Autofac;
using Lykke.SettingsReader;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Services;
using Lykke.Service.Iota.Api.Settings;
using Lykke.Service.Iota.Api.AzureRepositories;

namespace Lykke.Service.Iota.Api.Modules
{
    public class ServiceModule : Module
    {
        private readonly IReloadingManager<AppSettings> _settings;

        public ServiceModule(IReloadingManager<AppSettings> settings)
        {
            _settings = settings;
        }

        protected override void Load(ContainerBuilder builder)
        {
            var connectionStringManager = _settings.ConnectionString(x => x.IotaApi.Db.DataConnString);

            builder.RegisterInstance(_settings.CurrentValue.IotaApi)
                .As<IotaApiSettings>()
                .SingleInstance();

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

            builder.RegisterType<AddressInputRepository>()
                .As<IAddressInputRepository>()
                .WithParameter(TypedParameter.From(connectionStringManager))
                .SingleInstance();

            builder.RegisterType<AddressVirtualRepository>()
                .As<IAddressVirtualRepository>()
                .WithParameter(TypedParameter.From(connectionStringManager))
                .SingleInstance();

            builder.RegisterType<NodeClient>()
                .As<INodeClient>()
                .WithParameter("nodeUrl", _settings.CurrentValue.IotaApi.NodeUrl)
                .SingleInstance();

            builder.RegisterType<IotaService>()
                .As<IIotaService>()
                .WithParameter("minConfirmations", _settings.CurrentValue.IotaApi.MinConfirmations)
                .SingleInstance();
        }
    }
}
