using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.EntityFrameworkCore;
using BulkyBook.DataAccess.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.DataAccess.Repository;
using Microsoft.AspNetCore.Identity.UI.Services;
using BulkyBook.Utility;
using System.IO;
using AutoMapper;
using Stripe;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using BulkyBook.DataAccess.Initializer;

namespace BulkyBook
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            // Not included in repo. Just a configuration object with strings
            // related to Facebook, Google authentication, email settings, and Stripe
            // inside secrets.json
            SecretPropertiesConfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("secrets.json")
                .Build();
        }

        public IConfiguration Configuration { get; }
        public SecretProperties SecretProperties { get; }
        public IConfigurationRoot SecretPropertiesConfig { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // Used for dependency injection inside the app.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    Configuration.GetConnectionString("DefaultConnection")));
            services.AddIdentity<IdentityUser, IdentityRole>()
                .AddDefaultTokenProviders()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton<IEmailSender, EmailSender>();
            services.AddSingleton<ITempDataProvider, CookieTempDataProvider>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IDbInitializer, DbInitializer>();
            services.AddControllersWithViews().AddRazorRuntimeCompilation();
            services.AddRazorPages();

            services.Configure<StripeProperties>(SecretPropertiesConfig.GetSection("StripeProperties"));

            services.Configure<TwilioProperties>(SecretPropertiesConfig.GetSection("TwilioProperties"));

            // Needed for redirect on authorization
            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = $"/Identity/Account/Login";
                options.LogoutPath = $"/Identity/Account/Logout";
                options.AccessDeniedPath = $"/Identity/Account/AccessDenied";
            });

            SecretProperties secretPropertiesObject = SecretPropertiesConfig.Get<SecretProperties>();

            services.AddAuthentication().AddFacebook(options => {
                options.AppId = secretPropertiesObject.FacebookAppId;
                options.AppSecret = secretPropertiesObject.FacebookSecret;
            });

            services.AddAuthentication().AddGoogle(options => {
                options.ClientId = secretPropertiesObject.GoogleClientId;
                options.ClientSecret = secretPropertiesObject.GoogleClientSecret;
            });

            services.AddSession(options => {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // Auto Mapper Configurations
            // help from:
            // https://stackoverflow.com/questions/40275195/how-to-set-up-automapper-in-asp-net-core
            var mapperConfig = new MapperConfiguration(mc =>
            {
                mc.AddProfile(new MappingProfile());
            });

            IMapper mapper = mapperConfig.CreateMapper();
            services.AddSingleton(mapper);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IDbInitializer dbInitializer)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            dbInitializer.Initialize();

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            StripeConfiguration.ApiKey = 
                SecretPropertiesConfig.GetSection("StripeProperties")["StripeSecretKey"];

            app.UseSession();

            app.UseAuthentication();
            app.UseAuthorization();

            // Pattern changes if we use areas, default is customer
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });
        }
    }
}
