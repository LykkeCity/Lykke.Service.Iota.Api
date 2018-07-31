using Lykke.Service.BlockchainApi.Contract;
using Lykke.Service.BlockchainApi.Contract.Assets;
using Lykke.Service.Iota.Api.Core.Domain;
using Lykke.Service.Iota.Api.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/assets")]
    public class AssetsController : Controller
    {
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaginationResponse<AssetResponse>))]
        public IActionResult Get([Required, FromQuery] int take, [FromQuery] string continuation)
        {
            if (!ModelState.IsValidTakeParameter(take))
            {
                return BadRequest(ModelState.ToErrorResponse());
            }

            var assets = new AssetResponse[] { Asset.Miota.ToAssetResponse() };

            return Ok(PaginationResponse.From("", assets));
        }

        [HttpGet("{assetId}")]
        [ProducesResponseType(typeof(AssetResponse), StatusCodes.Status200OK)]
        public IActionResult GetAsset([Required] string assetId)
        {
            if(Asset.Miota.Id != assetId)
            {
                return NoContent();
            }

            return Ok(Asset.Miota.ToAssetResponse());
        }
    }
}
