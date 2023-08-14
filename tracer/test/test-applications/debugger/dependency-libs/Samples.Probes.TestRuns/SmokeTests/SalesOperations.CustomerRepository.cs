using System.Collections.Generic;

namespace Billing;

public static class CustomerRepository
{
    public static List<Customer> GetCustomersToProcess()
    {
        var goldenDeal = new Coupon { FancyName = "Golden Deal", DiscountRatio = 0.1 };
        var silverSteal = new Coupon { FancyName = "Silver Steal", DiscountRatio = 0.2 };
        var mysticZero = new Coupon { FancyName = "Mystic Zero", DiscountRatio = 1 };
        var bronzeBash = new Coupon { FancyName = "Bronze Bash", DiscountRatio = 0.05 };

        return new List<Customer>
        {
            new() { Id = 1, Name = "John", BranchName = "Central Plaza", Coupon = goldenDeal },
            new() { Id = 2, Name = "Doe", BranchName = "North Point", Coupon = silverSteal },
            new() { Id = 3, Name = "Jane", BranchName = "South Gate", Coupon = mysticZero },
            new() { Id = 4, Name = "Smith", BranchName = "West Avenue", Coupon = bronzeBash },
            new() { Id = 5, Name = "Marry", BranchName = "East Station", Coupon = goldenDeal },
            new() { Id = 6, Name = "Peter", BranchName = "Central Plaza", Coupon = silverSteal },
            new() { Id = 7, Name = "Lucy", BranchName = "North Point", Coupon = mysticZero },
            new() { Id = 8, Name = "Paul", BranchName = "West Avenue", Coupon = bronzeBash }
        };
    }
}
