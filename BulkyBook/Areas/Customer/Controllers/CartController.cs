using AutoMapper;
using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Stripe;
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
        private readonly IMapper mapper;

        public CartController(IUnitOfWork unitOfWork, 
            IEmailSender emailSender, 
            UserManager<IdentityUser> userManager,
            IMapper mapper)
        {
            this.unitOfWork = unitOfWork;
            this.emailSender = emailSender;
            this.userManager = userManager;
            this.mapper = mapper;
        }

        public IActionResult Index()
        {
            Claim claim = GetClaim();

            var shoppingCartVM = new ShoppingCartVM()
            {
                OrderHeader = new Models.OrderHeader(),

                // Include Product obj with shopping carts
                ListCart = unitOfWork.ShoppingCart
                    .GetAll(u => u.ApplicationUserId == claim.Value, includeProperties: "Product")
            };

            shoppingCartVM.OrderHeader.OrderTotal = 0;

            shoppingCartVM.OrderHeader.ApplicationUser = unitOfWork.ApplicationUser
                .GetFirstOrDefault(u => u.Id == claim.Value, includeProperties: "Company");

            FormatShoppingCart(shoppingCartVM);

            return View(shoppingCartVM);
        }

        [HttpPost]
        [ActionName("Index")]
        public async Task<IActionResult> IndexPOST()
        {
            Claim claim = GetClaim();
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

        public IActionResult Summary()
        {
            Claim claim = GetClaim();

            var shoppingCartVM = new ShoppingCartVM()
            {
                OrderHeader = new Models.OrderHeader(),
                ListCart = unitOfWork.ShoppingCart
                    .GetAll(c => c.ApplicationUserId == claim.Value, includeProperties: "Product")
            };

            shoppingCartVM.OrderHeader.ApplicationUser = unitOfWork.ApplicationUser
                .GetFirstOrDefault(c => c.Id == claim.Value, includeProperties: "Company");

            FormatShoppingCart(shoppingCartVM);

            shoppingCartVM.OrderHeader.Name = shoppingCartVM.OrderHeader.ApplicationUser.Name;

            //shoppingCartVM.OrderHeader = mapper
            //    .Map<ApplicationUser, OrderHeader>(shoppingCartVM.OrderHeader.ApplicationUser);

            shoppingCartVM.OrderHeader.Name = shoppingCartVM.OrderHeader.ApplicationUser.Name;
            shoppingCartVM.OrderHeader.PhoneNumber = shoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
            shoppingCartVM.OrderHeader.StreetAddress = shoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
            shoppingCartVM.OrderHeader.City = shoppingCartVM.OrderHeader.ApplicationUser.City;
            shoppingCartVM.OrderHeader.State = shoppingCartVM.OrderHeader.ApplicationUser.State;
            shoppingCartVM.OrderHeader.PostalCode = shoppingCartVM.OrderHeader.ApplicationUser.PostalCode;

            return View(shoppingCartVM);
        }

        [HttpPost]
        [ActionName("Summary")]
        [ValidateAntiForgeryToken]
        public IActionResult SummaryPost(ShoppingCartVM shoppingCartVM, string stripeToken)
        {
            Claim claim = GetClaim();

            shoppingCartVM.OrderHeader.ApplicationUser = unitOfWork.ApplicationUser
                .GetFirstOrDefault(c => c.Id == claim.Value, includeProperties: "Company");

            shoppingCartVM.ListCart = unitOfWork.ShoppingCart
                .GetAll(c => c.ApplicationUserId == claim.Value,includeProperties: "Product");

            shoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
            shoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
            shoppingCartVM.OrderHeader.ApplicationUserId = claim.Value;
            shoppingCartVM.OrderHeader.OrderDate = DateTime.Now;

            unitOfWork.OrderHeader.Add(shoppingCartVM.OrderHeader);
            unitOfWork.Save();

            List<OrderDetails> orderDetailsList = new List<OrderDetails>();

            foreach (var item in shoppingCartVM.ListCart)
            {
                item.Price = SetPrice(item);

                OrderDetails orderDetails = new OrderDetails()
                {
                    ProductId = item.ProductId,
                    OrderId = shoppingCartVM.OrderHeader.Id,
                    Price = item.Price,
                    Count = item.Count
                };
                shoppingCartVM.OrderHeader.OrderTotal += orderDetails.Count * orderDetails.Price;
                unitOfWork.OrderDetails.Add(orderDetails);
            }

            unitOfWork.ShoppingCart.RemoveRange(shoppingCartVM.ListCart);

            // Save in one call to remove cart and add order details
            unitOfWork.Save();
            HttpContext.Session.SetInt32(SD.ShoppingCartSession, 0);

            if (stripeToken == null)
            {
                //order will be created for delayed payment for authroized company
                shoppingCartVM.OrderHeader.PaymentDueDate = DateTime.Now.AddDays(30);
                shoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
                shoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
            }
            else
            {
                //process the payment
                var options = new ChargeCreateOptions
                {
                    Amount = Convert.ToInt32(shoppingCartVM.OrderHeader.OrderTotal * 100),
                    Currency = "usd",
                    Description = "Order ID : " + shoppingCartVM.OrderHeader.Id,
                    Source = stripeToken
                };

                var service = new ChargeService();

                // Makes charge on card
                Charge charge = service.Create(options);

                if (charge.Id == null)
                {
                    shoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusRejected;
                }
                else
                {
                    shoppingCartVM.OrderHeader.TransactionId = charge.Id;
                }
                if (charge.Status.ToLower() == "succeeded")
                {
                    shoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusApproved;
                    shoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
                    shoppingCartVM.OrderHeader.PaymentDate = DateTime.Now;
                }
            }

            unitOfWork.Save();

            return RedirectToAction("OrderConfirmation", "Cart", new { id = shoppingCartVM.OrderHeader.Id });

        }

        public IActionResult OrderConfirmation(int id)
        {
            return View(id);
        }

        private double SetPrice(ShoppingCart cart)
        {
            return SD.GetPriceBasedOnQuantity(cart.Count,
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

        private Claim GetClaim()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            return claim;
        }

        private void FormatShoppingCart(ShoppingCartVM shoppingCartVM)
        {
            foreach (var shoppingCart in shoppingCartVM.ListCart)
            {
                shoppingCart.Price = SetPrice(shoppingCart);

                shoppingCartVM.OrderHeader.OrderTotal += (shoppingCart.Price * shoppingCart.Count);

                shoppingCart.Product.Description = SD.ConvertToRawHtml(shoppingCart.Product.Description);

                if (shoppingCart.Product.Description.Length > 100)
                {
                    shoppingCart.Product.Description = shoppingCart.Product.Description.Substring(0, 99) + "...";
                }
            }
        }

    }

}
