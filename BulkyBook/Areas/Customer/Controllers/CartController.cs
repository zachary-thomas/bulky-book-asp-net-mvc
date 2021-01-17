using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BulkyBook.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class CartController : Controller
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IEmailSender emailSender;
        private readonly UserManager<IdentityUser> userManager;

        public ShoppingCartVM ShoppingCartVM { get; set; }

        public CartController(IUnitOfWork unitOfWork, 
            IEmailSender emailSender, 
            UserManager<IdentityUser> userManager)
        {
            this.unitOfWork = unitOfWork;
            this.emailSender = emailSender;
            this.userManager = userManager;
        }

        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            ShoppingCartVM = new ShoppingCartVM()
            {
                OrderHeader = new Models.OrderHeader(),

                // Include Product obj with shopping carts
                ListCart = unitOfWork.ShoppingCart
                    .GetAll(u => u.ApplicationUserId == claim.Value, includeProperties: "Product")
            };

            ShoppingCartVM.OrderHeader.OrderTotal = 0;

            ShoppingCartVM.OrderHeader.ApplicationUser = unitOfWork.ApplicationUser
                .GetFirstOrDefault(u => u.Id == claim.Value, includeProperties: "Company");

            foreach(var shoppingCart in ShoppingCartVM.ListCart)
            {
                shoppingCart.Price = SD.GetPriceBasedOnQuantity(shoppingCart.Count, 
                    shoppingCart.Product.Price, 
                    shoppingCart.Product.Price50, 
                    shoppingCart.Product.Price100);

                ShoppingCartVM.OrderHeader.OrderTotal += (shoppingCart.Price * shoppingCart.Count);

                shoppingCart.Product.Description = SD.ConvertToRawHtml(shoppingCart.Product.Description);

                if(shoppingCart.Product.Description.Length > 100)
                {
                    shoppingCart.Product.Description = shoppingCart.Product.Description.Substring(0, 99) + "...";
                }
            }

            return View(ShoppingCartVM);
        }

    }

}
