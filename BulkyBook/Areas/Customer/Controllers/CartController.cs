using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
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

        [HttpPost]
        [ActionName("Index")]
        public async Task<IActionResult> IndexPOST()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            var user = unitOfWork.ApplicationUser.GetFirstOrDefault(u => u.Id == claim.Value);

            if(user == null)
            {
                ModelState.AddModelError(string.Empty, "Verification email is empty.");
            }

            var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code = code },
                protocol: Request.Scheme);

            await emailSender.SendEmailAsync(user.Email, "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

            ModelState.AddModelError(string.Empty, "Verification email sent. Please check your email.");
            return RedirectToAction("Index");
        }

        public IActionResult Plus(int cartId)
        {
            var cart = unitOfWork.ShoppingCart.GetFirstOrDefault(c => c.Id == cartId, includeProperties: "Product");

            cart.Count += 1;

            SetPrice(cart);

            unitOfWork.Save();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Minus(int cartId)
        {
            var cart = unitOfWork.ShoppingCart.GetFirstOrDefault(c => c.Id == cartId, includeProperties: "Product");

            if(cart.Count == 1)
            {
                RemoveCart(cart);
            }
            else
            {
                cart.Count -= 1;

                SetPrice(cart);

                unitOfWork.Save();
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int cartId)
        {
            var cart = unitOfWork.ShoppingCart.GetFirstOrDefault(c => c.Id == cartId, includeProperties: "Product");

            RemoveCart(cart);

            return RedirectToAction(nameof(Index));
        }

        private void SetPrice(ShoppingCart cart)
        {
            SD.GetPriceBasedOnQuantity(cart.Count,
                cart.Product.Price,
                cart.Product.Price50,
                cart.Product.Price100);
        }

        private void RemoveCart(ShoppingCart cart)
        {
            var count = unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == cart.ApplicationUserId).ToList().Count;

            unitOfWork.ShoppingCart.Remove(cart);
            unitOfWork.Save();

            HttpContext.Session.SetInt32(SD.ShoppingCartSession, count - 1);
        }

    }

}
