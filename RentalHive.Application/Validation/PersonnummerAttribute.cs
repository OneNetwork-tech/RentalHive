using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace RentalHive.Application.Validation
{
    /// <summary>
    /// A custom validation attribute to validate a Swedish Personal Identity Number (Personnummer).
    /// It checks for correct format, length, and checksum using the Luhn algorithm.
    /// </summary>
    public class PersonnummerAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var personnummer = value as string;

            if (string.IsNullOrWhiteSpace(personnummer))
            {
                // This is handled by the [Required] attribute, but good to have a check.
                return ValidationResult.Success;
            }

            // 1. Normalize the number: remove hyphen and century part if present.
            string pnr = personnummer.Replace("-", "").Trim();
            if (pnr.Length == 12)
            {
                pnr = pnr.Substring(2);
            }

            // 2. Check length and if it's numeric.
            if (pnr.Length != 10 || !long.TryParse(pnr, out _))
            {
                return new ValidationResult("Invalid personnummer format. It should be 10 or 12 digits (YYMMDD-XXXX).");
            }

            // 3. Validate checksum using the Luhn algorithm.
            int sum = 0;
            for (int i = 0; i < pnr.Length; i++)
            {
                int digit = int.Parse(pnr[i].ToString());

                // Multiply every other digit by 2, starting from the first.
                if (i % 2 == 0)
                {
                    digit *= 2;
                    if (digit > 9)
                    {
                        digit = (digit % 10) + 1; // e.g., 16 becomes 1 + 6 = 7
                    }
                }
                sum += digit;
            }

            if (sum % 10 == 0)
            {
                return ValidationResult.Success; // The number is valid.
            }
            else
            {
                return new ValidationResult("Invalid personnummer. Please check the number and try again.");
            }
        }
    }
}
