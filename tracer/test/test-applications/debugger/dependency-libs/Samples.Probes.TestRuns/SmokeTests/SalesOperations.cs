using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Billing
{
    public partial class SalesOperations
    {
        public static double CalculateBill(SalesOperations.Customer customer, double productPrice)
        {
            double discountMultiplier = 1 - (customer.Coupon?.DiscountRatio ?? 0);
            return productPrice * discountMultiplier;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Run()
        {
            var customers = CustomerRepository.GetCustomersToProcess();

            double productPrice = 120;

            foreach (var customer in customers)
            {
                double billAmount = CalculateBill(customer, productPrice);
                string couponName = customer.Coupon?.FancyName ?? "No Coupon";
                PaymentProcessor.BillCustomer(customer, couponName, billAmount);
            }
        }

        public class Customer
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public Coupon Coupon { get; set; }
            public string BranchName { get; set; }
        }

        public class Coupon
        {
            public string FancyName { get; set; }
            public double DiscountRatio { get; set; } // 0.1 means 10% discount. The bug happens when it's 1.
        }
    }
}

