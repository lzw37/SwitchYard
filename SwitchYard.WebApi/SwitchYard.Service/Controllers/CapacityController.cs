using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    [Authorize]
    public class CapacityController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
