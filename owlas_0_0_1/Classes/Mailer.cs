using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Net.Mail;

namespace owlas_0_0_1.Classes
{
    public class Mailer
    {
        public void SendConfirmationEmail(MembershipUser user)
        {
            string confirmationGuid = user.ProviderUserKey.ToString();
            string verifyUrl = HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/account/verify?ID=" + confirmationGuid;

            var message = new MailMessage("owlas@owlas.com", user.Email)
            {
                Subject = "Owlas - Confirmação de email",
                Body = verifyUrl

            };

            var client = new SmtpClient("localhost");
            client.SendAsync(message, null);
        }
    }
}