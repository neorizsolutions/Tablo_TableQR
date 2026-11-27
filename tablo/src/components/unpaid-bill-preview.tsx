import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { QRCodeSVG } from "qrcode.react";
import { Printer } from "lucide-react";
import { toast } from "sonner";
import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";
import { formatINR } from "@/lib/mock-data";
import { parcelApi } from "@/services/parcel";
import { customerBillHtml } from "@/services/receipt-print";
import type { TableBill } from "@/services/service-desk";

export function UnpaidBillPreview({
  restaurantId,
  bill,
}: {
  restaurantId: string;
  bill: TableBill;
}) {
  const [open, setOpen] = useState(false);
  const { data, isFetching, isError } = useQuery({
    queryKey: ["unpaid-bill-upi", restaurantId, bill.id, bill.revision, bill.remainingAmount],
    queryFn: () => parcelApi.getBillUpiQr(restaurantId, bill.id),
    enabled: open,
    retry: false,
    gcTime: 0,
  });

  const qr =
    !isFetching && !isError && data?.billId === bill.id && data.amount === bill.remainingAmount
      ? data
      : undefined;

  const handlePrint = (width: "58mm" | "80mm") => {
    const html = customerBillHtml(bill, {
      paperWidth: width,
      payeeName: qr?.payeeName,
      payeeVpa: qr?.payeeVpa ?? "",
      upiReference: qr?.reference || bill.id,
    });
    const w = window.open("", "_blank", "width=450,height=700");
    if (w) {
      w.document.write(html);
      w.document.close();
      w.focus();
      setTimeout(() => {
        w.print();
      }, 250);
    } else {
      toast.error("Allow pop-ups for TABLO to print this bill.");
    }
  };

  return (
    <>
      <button
        type="button"
        className="float-right rounded-full border px-3 py-1 text-sm hover:bg-secondary transition"
        onClick={() => setOpen(true)}
      >
        Preview & print bill
      </button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-md">
          <DialogTitle>{bill.canSettle ? "Customer bill" : "Bill estimate"}</DialogTitle>
          <p className="text-xs text-muted-foreground">
            Table {bill.tableNumber}
            {bill.groupLabel} {bill.shareDescription}
          </p>
          <dl className="grid grid-cols-2 gap-2 text-sm my-2">
            <dt className="text-muted-foreground">Bill total</dt>
            <dd>{formatINR(bill.totalAmount)}</dd>
            <dt className="text-muted-foreground">Amount due</dt>
            <dd className="font-semibold text-foreground">{formatINR(bill.remainingAmount)}</dd>
          </dl>

          {isFetching && <p role="status" className="text-xs text-muted-foreground">Checking UPI payment details…</p>}
          {isError && <p role="status" className="text-xs text-muted-foreground">UPI payment is unavailable for this bill.</p>}
          {!isFetching && !isError && data && !qr && (
            <p role="alert" className="text-xs text-destructive">
              The balance changed. Close and refresh the bill before collecting payment.
            </p>
          )}

          {qr && (
            <section className="space-y-2 rounded-2xl border border-dashed border-border p-4 text-center">
              <p className="font-semibold text-sm">Pay {formatINR(qr.amount)} with UPI</p>
              <div className="flex justify-center py-1">
                <QRCodeSVG value={qr.upiUri} size={170} marginSize={3} title="Scan to pay this bill with UPI" />
              </div>
              <p className="text-xs text-muted-foreground font-medium">
                {qr.payeeName} · {qr.payeeVpa}
              </p>
              <p className="break-all text-[11px] text-muted-foreground">Reference: {qr.reference}</p>
              <a className="inline-block text-xs font-semibold text-emerald-600 underline" href={qr.upiUri}>
                Open UPI App
              </a>
            </section>
          )}

          <div className="mt-4 flex items-center justify-between border-t border-border pt-3">
            <span className="text-xs text-muted-foreground">Print paper size</span>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => handlePrint("58mm")}
                className="inline-flex items-center gap-1 rounded-full border border-border px-3 py-1 text-xs font-medium hover:bg-secondary"
              >
                <Printer className="h-3 w-3" /> 58mm
              </button>
              <button
                type="button"
                onClick={() => handlePrint("80mm")}
                className="inline-flex items-center gap-1 rounded-full border border-border px-3 py-1 text-xs font-medium hover:bg-secondary"
              >
                <Printer className="h-3 w-3" /> 80mm
              </button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
