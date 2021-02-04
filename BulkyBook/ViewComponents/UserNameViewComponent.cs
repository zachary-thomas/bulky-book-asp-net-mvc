using BulkyBook.DataAccess.Repository.IRepository;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BulkyBook.ViewComponents
{
    public class UserNameViewComponent : ViewComponent
    {
        private readonly IUnitOfWork unitOfWork;

        public UserNameViewComponent(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claims = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            return View(unitOfWork.ApplicationUser.GetFirstOrDefault(u => u.Id == claims.Value));
        }
    }
}
