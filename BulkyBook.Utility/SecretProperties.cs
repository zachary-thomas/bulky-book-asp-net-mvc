using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkyBook.Utility
{
    public class SecretProperties
    {
        public string SendGridKey { get; set; }
        public string SendGridUser { get; set; }

        public string FacebookAppId { get; set; }
        public string FacebookSecret { get; set; }

        public string GoogleClientId { get; set; }
        public string GoogleClientSecret { get; set; }
    }
}
