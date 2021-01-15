using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.DataAccess.Repository.IRepository;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Http;

namespace BulkyBook.Areas.Customer.Controllers
{
    // Have to explicitly define area
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public HomeController(ILogger<HomeController> logger, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            IEnumerable<Product> productList = _unitOfWork.Product.GetAll(includeProperties: "Category,CoverType");

            var claimsIdentty = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentty.FindFirst(ClaimTypes.NameIdentifier);

            if (claim != null)
            {
                var count = _unitOfWork.ShoppingCart
                    .GetAll(c => c.ApplicationUserId == claim.Value)
                    .ToList()
                    .Count();

                HttpContext.Session.SetInt32(SD.ShoppingCartSession, count);
            }

            return View(productList);
        }

        public IActionResult Details(int id)
        {
            var productFromDb = _unitOfWork.Product
                .GetFirstOrDefault(u => u.Id == id, includeProperties: "Category,CoverType");

            ShoppingCart cart = new ShoppingCart()
            {
                Product = productFromDb,
                ProductId = productFromDb.Id
            };

            return View(cart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize] // User must be logged in
        public IActionResult Details(ShoppingCart shoppingCart)
        {
            shoppingCart.Id = 0;

            if (ModelState.IsValid)
            {
                // Add to cart
                var claimsIdentty = (ClaimsIdentity)User.Identity;
                var claim = claimsIdentty.FindFirst(ClaimTypes.NameIdentifier);
                shoppingCart.ApplicationUserId = claim.Value;

                var cartFromDb = _unitOfWork.ShoppingCart.GetFirstOrDefault(
                    u => u.ApplicationUserId == shoppingCart.ApplicationUserId && u.ProductId == shoppingCart.ProductId,
                    includeProperties: "Product");

                if(cartFromDb == null)
                {
                    // Nothing in db for that product for the user
                    _unitOfWork.ShoppingCart.Add(shoppingCart);
                }
                else
                {
                    // Need ID from DB
                    cartFromDb.Count += shoppingCart.Count;

                    // Object from DB will still update if this statement is not included upon save.
                    _unitOfWork.ShoppingCart.Update(cartFromDb);
                }

                _unitOfWork.Save();

                var count = _unitOfWork.ShoppingCart
                    .GetAll(c => c.ApplicationUserId == shoppingCart.ApplicationUserId)
                    .ToList()
                    .Count();

                // Need extension to set entire object
                // HttpContext.Session.SetObject(SD.ShoppingCartSession, shoppingCart);

                // .net natively supports int
                HttpContext.Session.SetInt32(SD.ShoppingCartSession, count);

                // Getting the object from the session
                // var obj = HttpContext.Session.GetObject<ShoppingCart>(SD.ShoppingCartSession);

                return RedirectToAction(nameof(Index));
            }
            else
            {
                var productFromDb = _unitOfWork.Product
                    .GetFirstOrDefault(u => u.Id == shoppingCart.ProductId, includeProperties: "Category,CoverType");

                ShoppingCart cart = new ShoppingCart()
                {
                    Product = productFromDb,
                    ProductId = productFromDb.Id
                };

                return View(cart);
            }



        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
