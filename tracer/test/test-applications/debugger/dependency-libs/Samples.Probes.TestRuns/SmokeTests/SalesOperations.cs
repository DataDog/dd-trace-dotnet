using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Billing
{
    public  class SalesOperations
    {
        public static double CalculateBill(double productPrice, Coupon coupon)
        {
            double discountMultiplier = 1 - (coupon?.DiscountRatio ?? 0);
            return productPrice * discountMultiplier;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Run()
        {
            var customers = CustomerRepository.GetCustomersToProcess();

            double productPrice = 120;

            foreach (var customer in customers)
            {
                ShipOrder(customer, productPrice, customer.Coupon);
            }
        }

        private static void ShipOrder(Customer customer, double productPrice, Coupon coupon)
        {
            double billAmount = CalculateBill(productPrice, coupon);
            string couponName = coupon?.FancyName ?? "No Coupon";
            bool wasSuccessful = PaymentProcessor.BillCustomer(customer, couponName, billAmount);
        }
    }

    public class Coupon
    {
        public string FancyName { get; set; }
        public double DiscountRatio { get; set; } // 0.1 means 10% discount. The bug happens when it's 1.
    }

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Coupon Coupon { get; set; }
        public string BranchName { get; set; }
    }
}

