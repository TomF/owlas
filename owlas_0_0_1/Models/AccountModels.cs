using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Web.Mvc;
using System.Web.Security;

namespace owlas_0_0_1.Models
{

    public class ChangePasswordModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Palavra-passe actual")]
        public string OldPassword { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "A palavra-passe tem que ter no mínimo {2} caracteres", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Nova palavra-passe")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirme nova palavra-passe")]
        [Compare("NewPassword", ErrorMessage = "A palavra-passe e a confirmação não são iguais")]
        public string ConfirmPassword { get; set; }
    }

    public class LogOnModel
    {
        [Required]
        [ISTEmail]
        [Display(Name = "Endereço de email")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Palavra-passe")]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public class RegisterModel
    {
        [Required(ErrorMessage = "É necessário um email @ist.utl.pt")]
        [ISTEmail(ErrorMessage="É necessário um email @ist.utl.pt")]
        [Display(Name = "Endereço de email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "É necessário inserir uma palavra-passe")]
        [StringLength(100, ErrorMessage = "A palavra-passe tem que ter no mínimo {2} caracteres", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Palavra-passe")]
        public string Password { get; set; }
    }
}
