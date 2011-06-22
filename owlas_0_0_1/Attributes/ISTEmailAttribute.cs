using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace owlas_0_0_1.Models
{
    public class ISTEmailAttribute : ValidationAttribute, IClientValidatable
    {
        public override bool IsValid(object value)
        {
            var Email = Convert.ToString(value);
            if (string.IsNullOrEmpty(Email))
                return false;
            else
                return Email.EndsWith("@ist.utl.pt");
        }

        public IEnumerable<ModelClientValidationRule> GetClientValidationRules(ModelMetadata metadata, ControllerContext context)
        {
            yield return new ModelClientValidationRule
            {
                ErrorMessage = this.ErrorMessage,
                ValidationType = "istmail"
            };
        }
    }
}