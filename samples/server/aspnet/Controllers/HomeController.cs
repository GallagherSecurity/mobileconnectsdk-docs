﻿using Microsoft.AspNetCore.Mvc;

namespace GallagherUniversityStudentPortalSampleSite.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger) => _logger = logger;

        public IActionResult Index() => View();
    }
}
