using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;

namespace Tablo.Api.Services;

public record RecordBillPayment(Guid OperationId,string Method,decimal Amount,string? ExpectedRevision=null,decimal? ExpectedRemaining=null);
public record PaymentAcknowledgement(Guid PaymentId,Guid BillId,decimal Amount,string Method,decimal Remaining,DateTime CollectedAtUtc);

public class BillPaymentService(ApplicationDbContext db)
{
    public async Task<PaymentAcknowledgement> RecordAsync(Guid tenant,Guid actor,Guid billId,RecordBillPayment request)
    {
        await BillingService.RequireActorAsync(db,tenant,actor);
        if(request.OperationId==Guid.Empty || request.Method is not ("Cash" or "UPI" or "Card") || request.Amount<=0 || request.Amount!=decimal.Round(request.Amount,2) || request.Amount>9999999999999999.99m)
            throw new ArgumentException("Enter a positive payment amount with up to two decimal places, a supported method and an operation ID.");
        var hash=BillingService.Hash(JsonSerializer.Serialize(new{actor,billId,request.Method,Amount=request.Amount.ToString("G29",CultureInfo.InvariantCulture)}));
        var tableId=await db.TableBills.Where(b=>b.Id==billId && b.RestaurantId==tenant).Select(b=>(Guid?)b.TableId).SingleOrDefaultAsync()
            ??throw new KeyNotFoundException("Bill not found.");
        await using var tx=await TableOrderTransaction.BeginAsync(db);
        await TableOrderTransaction.LockAsync(db,tenant,tableId);
        var replay=await db.BillPayments.AsNoTracking().SingleOrDefaultAsync(p=>p.RestaurantId==tenant && p.OperationId==request.OperationId);
        if(replay!=null)
        {
            if(replay.PayloadHash!=hash) throw new ServiceConflictException("This payment operation was used for different details.");
            return JsonSerializer.Deserialize<PaymentAcknowledgement>(replay.AcknowledgementJson)!;
        }
        var bill=await db.TableBills.Include(b=>b.Orders).ThenInclude(o=>o.OrderItems).SingleAsync(b=>b.RestaurantId==tenant && b.Id==billId);
        if(bill.TableId!=tableId) throw new ServiceConflictException("This bill moved. Refresh before paying.");
        if(bill.SettledAtUtc!=null || bill.SplitAtUtc!=null) throw new ServiceConflictException("This bill is already settled.");
        var review=BillingService.Project(bill);
        if(!review.CanSettle) throw new ServiceConflictException("Serve all chargeable food before recording payment.");
        if(await db.Orders.AnyAsync(o=>o.RestaurantId==tenant && o.TableId==tableId && o.DiningGroupId==bill.DiningGroupId && o.TableBillId==null && o.Status!="Served" && o.Status!="Cancelled" && o.Status!="Rejected"))
            throw new ServiceConflictException("Attach the table's active legacy orders before recording payment.");
        var payments=await db.BillPayments.Where(p=>p.RestaurantId==tenant && p.TableBillId==billId).OrderBy(p=>p.CollectedAtUtc).ThenBy(p=>p.Id).ToListAsync();
        if(bill.PaymentRevision!=null && bill.PaymentRevision!=review.Revision) throw new ServiceConflictException("Bill contents changed after payments began.");
        var remaining=review.TotalAmount-payments.Sum(p=>p.Amount);
        if((request.ExpectedRevision!=null && request.ExpectedRevision!=review.Revision) || (request.ExpectedRemaining.HasValue && request.ExpectedRemaining.Value!=remaining)) throw new ServiceConflictException("The bill or its balance changed. Review it again.");
        if(request.Amount>remaining) throw new ServiceConflictException("Payment exceeds the remaining bill balance.");
        bill.PaymentRevision=review.Revision;
        var payment=new BillPayment{RestaurantId=tenant,TableBillId=billId,ActorEmployeeId=actor,OperationId=request.OperationId,
            TableIdSnapshot=bill.TableId,TableNumberSnapshot=bill.TableNumberSnapshot,GroupLabelSnapshot=bill.GroupLabelSnapshot,
            PayloadHash=hash,Method=request.Method,Amount=request.Amount};
        var acknowledgement=new PaymentAcknowledgement(payment.Id,billId,payment.Amount,payment.Method,remaining-payment.Amount,payment.CollectedAtUtc);
        payment.AcknowledgementJson=JsonSerializer.Serialize(acknowledgement);db.BillPayments.Add(payment);payments.Add(payment);
        if(acknowledgement.Remaining==0)
        {
            bill.SettledAtUtc=payment.CollectedAtUtc;bill.SettledByEmployeeId=actor;
            bill.SettlementOperationId=request.OperationId;bill.SettlementHash=hash;
            bill.Subtotal=review.Subtotal;bill.Tax=review.Tax;bill.Discount=review.Discount;bill.TotalAmount=review.TotalAmount;
            bill.PaymentMethod=payments.Select(p=>p.Method).Distinct().Count()==1?request.Method:"Mixed";
            review.Status="Settled";review.CanSettle=false;review.SettledAtUtc=bill.SettledAtUtc;review.PaymentMethod=bill.PaymentMethod;
            review.RestaurantName=await db.Restaurants.Where(r=>r.Id==tenant).Select(r=>r.Name).SingleAsync();review.SettledByEmployeeId=actor;
            review.PaidAmount=review.TotalAmount;review.RemainingAmount=0;
            review.Payments=payments.Select(p=>new BillPaymentDto(p.Id,p.Method,p.Amount,p.CollectedAtUtc,p.ActorEmployeeId)).ToList();
            bill.ReceiptJson=JsonSerializer.Serialize(review);
            var requests=await BillingService.OutstandingBillRequestsAsync(db,bill);
            foreach(var sr in requests){sr.Status="Completed";sr.UpdatedAt=payment.CollectedAtUtc;}
            await new InventoryService(db).DeductForSettlementAsync(db, tenant, billId, actor, request.OperationId);
        }
        await db.SaveChangesAsync();if(tx!=null) await tx.CommitAsync();return acknowledgement;
    }
}
