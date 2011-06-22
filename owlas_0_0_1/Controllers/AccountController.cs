using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using owlas_0_0_1.Models;
using System.Text.RegularExpressions;

namespace owlas_0_0_1.Controllers
{
    public class AccountController : Controller
    {

        //
        // GET: /Account/LogOn

        public ActionResult LogOn()
        {
            return View();
        }

        //
        // POST: /Account/LogOn

        [HttpPost]
        public ActionResult LogOn(LogOnModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                if (Membership.ValidateUser(model.Email, model.Password))
                {
                    FormsAuthentication.SetAuthCookie(model.Email, model.RememberMe);
                    if (Url.IsLocalUrl(returnUrl) && returnUrl.Length > 1 && returnUrl.StartsWith("/")
                        && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\"))
                    {
                        return Redirect(returnUrl);
                    }
                    else
                    {
                        return RedirectToAction("About", "Home");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "The user name or password provided is incorrect.");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/LogOff

        public ActionResult LogOff()
        {
            FormsAuthentication.SignOut();

            return RedirectToAction("Index", "Home");
        }

        //
        // GET: /Account/Register

        public ActionResult Register()
        {
            return View();
        }

        //
        // POST: /Account/Register

        [HttpPost]
        public ActionResult Register(RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                // Attempt to register the user
                MembershipCreateStatus createStatus;
                Membership.CreateUser(model.Email, model.Password, model.Email, null, null, true, null, out createStatus);

                if (createStatus == MembershipCreateStatus.Success)
                {
                    MembershipUser user = Membership.GetUser(model.Email, false);
                    user.IsApproved = false;
                    Membership.UpdateUser(user);
                    owlas_0_0_1.Classes.Mailer mailer = new Classes.Mailer();
                    mailer.SendConfirmationEmail(user);

                    /*FormsAuthentication.SetAuthCookie(model.Email, false  createPersistentCookie );*/
                    return RedirectToAction("About", "Home");
                }
                else
                {
                    ModelState.AddModelError(model.Email, ErrorCodeToString(createStatus));
                }
            }

            // If we got this far, something failed, redisplay form
            return View("~/Views/Home/Index.cshtml", model);
        }

        //
        // GET: /Account/ChangePassword

        [Authorize]
        public ActionResult ChangePassword()
        {
            return View();
        }

        //
        // POST: /Account/ChangePassword

        [Authorize]
        [HttpPost]
        public ActionResult ChangePassword(ChangePasswordModel model)
        {
            if (ModelState.IsValid)
            {

                // ChangePassword will throw an exception rather
                // than return false in certain failure scenarios.
                bool changePasswordSucceeded;
                try
                {
                    MembershipUser currentUser = Membership.GetUser(User.Identity.Name, true /* userIsOnline */);
                    changePasswordSucceeded = currentUser.ChangePassword(model.OldPassword, model.NewPassword);
                }
                catch (Exception)
                {
                    changePasswordSucceeded = false;
                }

                if (changePasswordSucceeded)
                {
                    return RedirectToAction("ChangePasswordSuccess");
                }
                else
                {
                    ModelState.AddModelError("", "The current password is incorrect or the new password is invalid.");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/ChangePasswordSuccess

        public ActionResult ChangePasswordSuccess()
        {
            return View();
        }

        //
        // GET: /Account/Verify/ID

        public ActionResult Verify(string ID)
        {
            if (string.IsNullOrEmpty(ID) || (!Regex.IsMatch(ID, @"[0-9a-f]{8}\-([0-9a-f]{4}\-){3}[0-9a-f]{12}")))
            {
                TempData["tempMessage"] = "The user account is not valid. Please try clicking the link in your email again.";
                return View();
            }

            else
            {
                MembershipUser user = Membership.GetUser(new Guid(ID));

                if (!user.IsApproved)
                {
                    user.IsApproved = true;
                    Membership.UpdateUser(user);
                    FormsAuthentication.SetAuthCookie(user.Email, false);
                    return RedirectToAction("About", "Home");
                }
                else
                {
                    FormsAuthentication.SignOut();
                    return RedirectToAction("Index", "Home");
                }
            }
        }


        #region Status Codes
        private static string ErrorCodeToString(MembershipCreateStatus createStatus)
        {
            // See http://go.microsoft.com/fwlink/?LinkID=177550 for
            // a full list of status codes.
            switch (createStatus)
            {
                case MembershipCreateStatus.DuplicateUserName:
                    return "O endereço de email escolhido já está registado";

                case MembershipCreateStatus.InvalidPassword:
                    return "A palavra-passe escolhida é inválida";

                case MembershipCreateStatus.InvalidUserName:
                    return "O endereço de email escolhido é inválido";

                default:
                    return "Houve um erro com o registo. Se o erro persistir contacte o administrador do site";
            }
        }
        #endregion
    }
}
