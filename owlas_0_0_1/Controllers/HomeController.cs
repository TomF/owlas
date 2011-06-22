﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace owlas_0_0_1.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            if (Request.IsAuthenticated)
            {
                return RedirectToAction("About", "Home");
            }
            return View();
        }

        public ActionResult About()
        {
            return View();
        }
    }
}
