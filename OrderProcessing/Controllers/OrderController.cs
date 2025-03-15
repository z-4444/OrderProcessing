using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Models;

namespace OrderProcessing.Controllers
{
    public class OrderController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View(new Order());
        }

        [HttpPost]
        public IActionResult Result(Order order)
        {
            if (!ModelState.IsValid)
                return View("Index", order);

            return View("Result", order);
        }
    }
}