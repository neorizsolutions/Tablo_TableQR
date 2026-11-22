namespace Tablo.Api.Services;

public record SplitItem(Guid Id,int Quantity,decimal UnitPrice);
public record BillShare(decimal Subtotal,decimal Tax,decimal Discount)
{
    public decimal Total=>Subtotal+Tax-Discount;
}

// Pure allocation arithmetic: original kitchen quantities and stored financial totals are inputs,
// never rewritten. All allocation operates in paise and conserves each component exactly.
public static class BillSplitCalculator
{
    private static long Cents(decimal value)
    {
        if(value<0 || value!=decimal.Round(value,2) || value>9999999999999999.99m)
            throw new ArgumentException("Amounts must be nonnegative and have at most two decimal places.");
        return checked((long)(value*100));
    }

    private static long[] Allocate(long amount,long[] weights,long[]? capacities=null)
    {
        var result=new long[weights.Length];
        var remaining=amount;
        while(remaining>0)
        {
            var eligible=Enumerable.Range(0,weights.Length).Where(i=>weights[i]>0 && (capacities==null || result[i]<capacities[i])).ToArray();
            if(eligible.Length==0) throw new ArgumentException("These shares cannot accommodate the recorded amounts.");
            var sum=eligible.Sum(i=>(decimal)weights[i]);
            var fractions=new List<(int Index,decimal Remainder)>();
            var budget=remaining;
            foreach(var i in eligible)
            {
                var exact=budget*(decimal)weights[i]/sum;
                var take=(long)decimal.Floor(exact);
                if(capacities!=null) take=Math.Min(take,capacities[i]-result[i]);
                result[i]+=take; remaining-=take;
                fractions.Add((i,exact-decimal.Floor(exact)));
            }
            foreach(var entry in fractions.OrderByDescending(x=>x.Remainder).ThenBy(x=>x.Index))
            {
                if(remaining==0) break;
                if(capacities!=null && result[entry.Index]>=capacities[entry.Index]) continue;
                result[entry.Index]++; remaining--;
            }
        }
        return result;
    }

    public static IReadOnlyList<BillShare> ByAmounts(decimal subtotal,decimal tax,decimal discount,IReadOnlyList<decimal> amounts)
    {
        var sub=Cents(subtotal); var vat=Cents(tax); var off=Cents(discount);
        if(amounts.Count is <1 or >100 || amounts.Any(a=>a<=0)) throw new ArgumentException("Enter between one and 100 positive guest shares.");
        var totals=amounts.Select(Cents).ToArray();
        if(totals.Sum()!=sub+vat-off) throw new ArgumentException("Guest shares must equal the original bill total exactly.");
        var discounts=Allocate(off,totals);
        var gross=totals.Select((t,i)=>checked(t+discounts[i])).ToArray();
        var taxes=Allocate(vat,gross,gross);
        return totals.Select((_,i)=>new BillShare((gross[i]-taxes[i])/100m,taxes[i]/100m,discounts[i]/100m)).ToArray();
    }

    public static IReadOnlyList<BillShare> ByItems(IReadOnlyList<SplitItem> items,decimal tax,decimal discount,IReadOnlyList<Dictionary<Guid,int>> shares)
    {
        if(items.Count==0 || items.Select(i=>i.Id).Distinct().Count()!=items.Count || items.Any(i=>i.Quantity<=0) || shares.Count is <2 or >100)
            throw new ArgumentException("Select valid source items and at least two item shares.");
        var lookup=items.ToDictionary(i=>i.Id);
        var weights=new long[shares.Count];
        var allocated=items.ToDictionary(i=>i.Id,_=>0L);
        for(var i=0;i<shares.Count;i++)
        {
            if(shares[i].Count==0) throw new ArgumentException("Every item share must contain items.");
            foreach(var pair in shares[i])
            {
                if(pair.Value<=0 || !lookup.TryGetValue(pair.Key,out var item)) throw new ArgumentException("An allocation contains an unknown item or invalid quantity.");
                allocated[pair.Key]+=pair.Value;
                weights[i]=checked(weights[i]+Cents(item.UnitPrice)*pair.Value);
            }
        }
        if(items.Any(item=>allocated[item.Id]!=item.Quantity)) throw new ArgumentException("Allocate every source quantity exactly once.");
        var vat=Cents(tax); var off=Cents(discount);
        // Zero-price items may be allocated; when the entire bill is free use quantity weights.
        var basis=weights.Any(w=>w>0)?weights:shares.Select(s=>s.Values.Sum(v=>(long)v)).ToArray();
        var taxes=Allocate(vat,basis);
        var gross=weights.Select((w,i)=>checked(w+taxes[i])).ToArray();
        var discounts=Allocate(off,gross,gross);
        return weights.Select((w,i)=>new BillShare(w/100m,taxes[i]/100m,discounts[i]/100m)).ToArray();
    }
}
