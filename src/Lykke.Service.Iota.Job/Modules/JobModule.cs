using Autofac;
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
        private readonly IReloadingManager<AppSettings> _settings;

        public JobModule(IReloadingManager<AppSettings> settings)
        {
            _settings = settings;
        }

        protected override void Load(ContainerBuilder builder)
        {
            var connectionStringManager = _settings.ConnectionString(x => x.IotaJob.Db.DataConnString);

            builder.RegisterInstance(_settings.CurrentValue.IotaJob)
                .As<IotaJobSettings>()
                .SingleInstance();

            builder.RegisterChaosKitty(_settings.CurrentValue.IotaJob.ChaosKitty);

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
                .WithParameter(TypedParameter.From(_settings.CurrentValue.IotaJob.Node))
                .SingleInstance();

            builder.RegisterType<IotaService>()
                .As<IIotaService>()
                .SingleInstance();

            builder.RegisterType<BalanceHandler>()
                .As<IStartable>()
                .AutoActivate()
                .WithParameter("period", _settings.CurrentValue.IotaJob.BalanceCheckerInterval)
                .SingleInstance();

            builder.RegisterType<BroadcastHandler>()
                .As<IStartable>()
                .AutoActivate()
                .WithParameter("period", _settings.CurrentValue.IotaJob.BroadcastCheckerInterval)
                .SingleInstance();

            builder.RegisterType<PromotionHandler>()
                .As<IStartable>()
                .AutoActivate()
                .WithParameter("period", _settings.CurrentValue.IotaJob.PromotionHandlerInterval)
                .SingleInstance();

            builder.RegisterType<ReattachmentHandler>()
                .As<IStartable>()
                .AutoActivate()
                .WithParameter("period", _settings.CurrentValue.IotaJob.ReattachmentHandlerInterval)
                .SingleInstance();            
        }
    }
}
