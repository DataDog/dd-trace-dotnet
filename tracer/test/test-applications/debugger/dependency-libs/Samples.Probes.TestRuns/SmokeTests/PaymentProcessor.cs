using System;

namespace Billing;

public class PaymentProcessor
{
    public static void BillCustomer(SalesOperations.Customer customer, string couponName, double billAmount)
    {
        Console.WriteLine($"Bill for {customer.Name} (Coupon: {couponName}): ${billAmount}");
    }
}
