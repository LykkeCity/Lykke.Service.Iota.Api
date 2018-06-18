using Common;
using Lykke.Common.Api.Contract.Responses;
using Lykke.Service.BlockchainApi.Contract;
using Lykke.Service.BlockchainApi.Contract.Assets;
using Lykke.Service.BlockchainApi.Contract.Balances;
using Lykke.Service.BlockchainApi.Contract.Transactions;
using Lykke.Service.Iota.Api.Core.Domain;
using Lykke.Service.Iota.Api.Core.Domain.Balance;
using Lykke.Service.Iota.Api.Core.Domain.Broadcast;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lykke.Service.Iota.Api.Helpers
{
    public static class Extenstions
    {
        public static ErrorResponse ToErrorResponse(this ModelStateDictionary modelState)
        {
            var response = new ErrorResponse();

            foreach (var state in modelState)
            {
                var messages = state.Value.Errors
                    .Where(e => !string.IsNullOrWhiteSpace(e.ErrorMessage))
                    .Select(e => e.ErrorMessage)
                    .Concat(state.Value.Errors
                        .Where(e => string.IsNullOrWhiteSpace(e.ErrorMessage))
                        .Select(e => e.Exception.Message))
                    .ToList();

                response.ModelErrors.Add(state.Key, messages);
            }

            return response;
        }

        public static AssetResponse ToAssetResponse(this Asset self)
        {
            return new AssetResponse
            {
                Accuracy = self.Accuracy,
                Address = string.Empty,
                AssetId = self.Id,
                Name = self.Id
            };
        }

        public static BroadcastedTransactionState ToBroadcastedTransactionState(this BroadcastState self)
        {
            switch (self)
            {
                case BroadcastState.InProgress:
                    return BroadcastedTransactionState.InProgress;
                case BroadcastState.Completed:
                    return BroadcastedTransactionState.Completed;
                case BroadcastState.Failed:
                    return BroadcastedTransactionState.Failed;
                default:
                    throw new ArgumentException($"Failed to convert " +
                        $"{nameof(BroadcastState)}.{Enum.GetName(typeof(BroadcastState), self)} " +
                        $"to {nameof(BroadcastedTransactionState)}");
            }
        }

        public static DateTime GetTimestamp(this IBroadcast self)
        {
            switch (self.State)
            {
                case BroadcastState.InProgress:
                    return self.BroadcastedUtc.Value;
                case BroadcastState.Completed:
                    return self.CompletedUtc.Value;
                case BroadcastState.Failed:
                    return self.FailedUtc.Value;
                default:
                    throw new ArgumentException($"Unsupported IBroadcast.State={Enum.GetName(typeof(BroadcastState), self.State)}");
            }
        }

        public static WalletBalanceContract ToWalletBalanceContract(this IBalancePositive self)
        {
            return new WalletBalanceContract
            {
                Address = self.Address,
                AssetId = Asset.Miota.Id,
                Balance = Conversions.CoinsToContract(self.Amount, Asset.Miota.Accuracy),
                Block = self.Block
            };
        }
    }
}
