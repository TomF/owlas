using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using MailgunClient;
using System.Text;

namespace owlas_0_0_1.Classes
{
    public class Mailer
    {
        public void SendConfirmationEmail(MembershipUser user)
        {
            Mailgun.Init("key-4otkg7cfsi5qn9f$e9");

            string confirmationGuid = user.ProviderUserKey.ToString();
            string verifyUrl = HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/account/verify?ID=" + confirmationGuid;
            string sender = "noreply@owlasdev.mailgun.org";
            string recipients = user.Email;

            MailgunMessage.SendText(sender, recipients, "Owlas - Confirmação de registo", "Para confirmares o teu endereço de email e ires para o owlas carrega neste link:\n\n" + verifyUrl + "\n\n\nA equipa owlas.com");
        }
    }
}