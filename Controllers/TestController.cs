using Microsoft.AspNetCore.Mvc;

namespace BuildWorkflowRelease.Controllers
{
    public class TestController : Controller
    {
        [HttpGet]
        public IActionResult Health()
        {
            return Content("OK", "text/plain");
        }
    }
}