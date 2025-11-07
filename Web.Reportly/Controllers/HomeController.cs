using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Web.Reportly.Models;
using Web.Reportly.Controllers.Filters;

namespace Web.Reportly.Controllers
{
    //Kế thừa từ BaseController
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "Dashboard";
            ViewData["PageTitle"] = "Dashboard";
            ViewData["PageSubtitle"] = "Chào mừng bạn";
            return View();
        }

        public IActionResult Privacy()
        {
            return Ok();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}