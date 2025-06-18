using Microsoft.AspNetCore.Mvc;

namespace WebSocketServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Health()
        {
            return Ok("Healthy");
        }
    }
}
