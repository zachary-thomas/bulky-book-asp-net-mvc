using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BulkyBook.Utility
{
    public class EmailSender : IEmailSender
    {
        private readonly SecretProperties secretProperties;

        public EmailSender(IOptions<SecretProperties> options)
        {
            secretProperties = options.Value;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var client = new SendGridClient(secretProperties.SendGridKey);
            var from = new EmailAddress("test@gmail.com", "Bulky Books");
            var to = new EmailAddress(email, "End User");

            // Empty quotes is for plain text
            var msg = MailHelper.CreateSingleEmail(from, to, subject, "", htmlMessage);
            return client.SendEmailAsync(msg);
        }

    }

}
