using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public class BillSplitCalculatorTests
{
    [Fact]
    public void AmountShares_PreserveEveryPaiseOfStoredTaxAndDiscount()
    {
        var parts=BillSplitCalculator.ByAmounts(100m,5m,1m,[34.67m,34.67m,34.66m]);
        Assert.Equal(100m,parts.Sum(p=>p.Subtotal));
        Assert.Equal(5m,parts.Sum(p=>p.Tax));
        Assert.Equal(1m,parts.Sum(p=>p.Discount));
        Assert.Equal(new[]{34.67m,34.67m,34.66m},parts.Select(p=>p.Total).ToArray());
        Assert.All(parts,p=>Assert.Equal(p.Total,p.Subtotal+p.Tax-p.Discount));
    }

    [Fact]
    public void ItemShares_SplitQuantityWithoutRecalculatingTax()
    {
        var item=Guid.NewGuid();
        var parts=BillSplitCalculator.ByItems([new(item,3,1m)],1m,0m,
            [new Dictionary<Guid,int>{{item,1}},new Dictionary<Guid,int>{{item,2}}]);
        Assert.Equal(4m,parts.Sum(p=>p.Total));
        Assert.Equal(1m,parts[0].Subtotal); Assert.Equal(2m,parts[1].Subtotal);
        Assert.Equal(.33m,parts[0].Tax); Assert.Equal(.67m,parts[1].Tax);
    }

    [Fact]
    public void Split_RejectsLostDuplicatedForeignAndFractionalMoneyAllocations()
    {
        var item=Guid.NewGuid();
        Assert.Throws<ArgumentException>(()=>BillSplitCalculator.ByAmounts(10,0,0,[5m,4m]));
        Assert.Throws<ArgumentException>(()=>BillSplitCalculator.ByAmounts(10,0,0,[0m,10m]));
        Assert.Throws<ArgumentException>(()=>BillSplitCalculator.ByAmounts(10,0,0,[5.001m,4.999m]));
        Assert.Throws<ArgumentException>(()=>BillSplitCalculator.ByItems([new(item,2,5)],0,0,
            [new Dictionary<Guid,int>{{item,2}},new Dictionary<Guid,int>{{item,1}}]));
        Assert.Throws<ArgumentException>(()=>BillSplitCalculator.ByItems([new(item,2,5)],0,0,
            [new Dictionary<Guid,int>{{item,1}},new Dictionary<Guid,int>{{Guid.NewGuid(),1}}]));
    }

    [Fact]
    public void Rounding_WithTinySharesAndDiscountNeverCreatesNegativeComponents()
    {
        var parts=BillSplitCalculator.ByAmounts(.01m,.01m,.01m,[.01m]);
        Assert.Equal(.01m,parts.Single().Total);
        var many=BillSplitCalculator.ByAmounts(.01m,.99m,0m,Enumerable.Repeat(.01m,100).ToArray());
        Assert.All(many,p=>Assert.True(p.Subtotal>=0 && p.Tax>=0 && p.Discount>=0));
        Assert.Equal(.99m,many.Sum(p=>p.Tax));
    }
}
