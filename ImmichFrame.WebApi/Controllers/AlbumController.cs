using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImmichFrame.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AlbumController : ControllerBase
    {
        private readonly ILogger<AlbumController> _logger;
        private readonly IImmichFrameLogic _logic;

        public AlbumController(ILogger<AlbumController> logger, IImmichFrameLogic logic)
        {
            _logger = logger;
            _logic = logic;
        }

        /// <summary>
        /// The configured albums of every named account, so a client can show them one at a time
        /// by requesting assets with <c>accountName</c> and <c>albumIds</c>.
        /// </summary>
        [HttpGet(Name = "GetConfiguredAlbums")]
        public async Task<List<ConfiguredAlbum>> GetConfiguredAlbums(string clientIdentifier = "")
        {
            _logger.LogDebug("Configured albums requested by '{clientIdentifier}'", clientIdentifier.SanitizeString());
            return (await _logic.GetConfiguredAlbums()).ToList();
        }
    }
}
