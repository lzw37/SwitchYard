using Microsoft.AspNetCore.Mvc;

namespace SwitchYard.Service.Controllers
{
    public class HumpController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
