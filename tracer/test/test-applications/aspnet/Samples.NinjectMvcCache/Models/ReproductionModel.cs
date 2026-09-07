using System;
using System.ComponentModel.DataAnnotations;

namespace Samples.NinjectMvcCache.Models
{
    public sealed class ReproductionModel
    {
        public int Value01 { get; set; }

        public int Value02 { get; set; }

        public int Value03 { get; set; }

        public int Value04 { get; set; }

        public int Value05 { get; set; }

        public int Value06 { get; set; }

        public int Value07 { get; set; }

        public int Value08 { get; set; }

        public int Value09 { get; set; }

        public int Value10 { get; set; }

        public long Value11 { get; set; }

        public long Value12 { get; set; }

        public DateTime Value13 { get; set; }

        public DateTime Value14 { get; set; }

        public decimal Value15 { get; set; }

        public decimal Value16 { get; set; }

        [NoDependencyValidation]
        public string Text { get; set; }
    }

    public sealed class NoDependencyValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            return true;
        }
    }
}
