using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace owlas_0_0_1.Models
{
    public class EmailAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            var Email = Convert.ToString(value);
            if (string.IsNullOrEmpty(Email))
                return false;
            else
                return Email.EndsWith("@ist.utl.pt");
        }

        public override string FormatErrorMessage(string name)
        {
            return "Precisa de um " + name + " do Instituto Superior Técnico.";
        }
    }
}