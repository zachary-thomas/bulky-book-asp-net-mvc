using AutoMapper;
using BulkyBook.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BulkyBook
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<OrderHeader, ApplicationUser>();
            CreateMap<ApplicationUser, OrderHeader>();
        }
    }
}
