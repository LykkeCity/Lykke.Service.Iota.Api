using Autofac;
using Common.Log;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Services;
using Lykke.SettingsReader;
using Lykke.Service.Iota.Job.PeriodicalHandlers;
using Lykke.Service.Iota.Job.Settings;
using Lykke.Service.Iota.Job.Services;
using Lykke.Common.Chaos;
using Lykke.Service.Iota.Api.AzureRepositories;

namespace Lykke.Service.Iota.Job.Modules
{
    public class JobModule : Module
    {
        private readonly IReloadingManager<IotaJobSettings> _settings;
        private readonly ILog _log;

        public JobModule(IReloadingManager<IotaJobSettings> settings, ILog log)
        {
            _settings = settings;
            _log = log;
        }

        protected override void Load(ContainerBuilder builder)
        {
            var connectionStringManager = _settings.ConnectionString(x => x.Db.DataConnString);

            builder.RegisterInstance(_settings.CurrentValue)
                .As<IotaJobSettings>()
                .SingleInstance();

            builder.RegisterChaosKitty(_settings.CurrentValue.ChaosKitty);

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

            builder.RegisterType<AddressRepository>()
                .As<IAddressRepository>()
                .WithParameter(TypedParameter.From(connectionStringManager))
                .SingleInstance();

            builder.RegisterType<AddressInputRepository>()
                .As<IAddressInputRepository>()
                .WithParameter(TypedParameter.From(connectionStringManager))
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

            builder.RegisterType<PeriodicalService>()
                .As<IPeriodicalService>()
                .SingleInstance();

            builder.RegisterType<NodeClient>()
                .As<INodeClient>()
                .WithParameter("nodeUrl", _settings.CurrentValue.NodeUrl)
                .SingleInstance();

            builder.RegisterType<IotaService>()
                .As<IIotaService>()
                .WithParameter("minConfirmations", _settings.CurrentValue.MinConfirmations)
                .SingleInstance();

            builder.RegisterType<BalanceHandler>()
                .AutoActivate()
                .WithParameter("period", _settings.CurrentValue.BalanceCheckerInterval)
                .SingleInstance();

            builder.RegisterType<BroadcastHandler>()
                .AutoActivate()
                .WithParameter("period", _settings.CurrentValue.BroadcastCheckerInterval)
                .SingleInstance();

            builder.RegisterType<PromotionHandler>()
                .AutoActivate()
                .WithParameter("period", _settings.CurrentValue.PromotionHandlerInterval)
                .SingleInstance();

            builder.RegisterType<ReattachmentHandler>()
                .AutoActivate()
                .WithParameter("period", _settings.CurrentValue.ReattachmentHandlerInterval)
                .SingleInstance();            
        }
    }
}
