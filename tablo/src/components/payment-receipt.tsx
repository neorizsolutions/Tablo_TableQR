import { useRef, useState } from "react";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "@/components/ui/dialog";
import { receiptHtml } from "@/services/receipt-print";
import type { TableBill } from "@/services/service-desk";

export function PaymentReceipt({ bill }: { bill: TableBill }) {
  const [open, setOpen] = useState(false);
  const frame = useRef<HTMLIFrameElement>(null);
  if (bill.status !== "Settled") return <p className="mt-3 text-sm">Partial collections: {bill.paidAmount?.toFixed(2)} · Remaining: {bill.remainingAmount?.toFixed(2)}. Final receipt is available after full settlement.</p>;
  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="mt-3 rounded-full border px-4 py-2 text-sm font-semibold"
      >
        View / print receipt
      </button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-2xl bg-card p-6">
          <DialogTitle>Payment receipt · Table {bill.tableNumber}</DialogTitle>
          <DialogDescription>
            Recorded settlement details. Print or save as PDF using your browser.
          </DialogDescription>
          <iframe
            ref={frame}
            title="Payment receipt preview"
            srcDoc={receiptHtml(bill)}
            className="h-[55vh] w-full rounded-xl border bg-white"
          />
          <button
            type="button"
            onClick={() => {
              frame.current?.contentWindow?.focus();
              frame.current?.contentWindow?.print();
            }}
            className="rounded-full bg-primary px-4 py-2 font-semibold text-primary-foreground"
          >
            Print receipt
          </button>
        </DialogContent>
      </Dialog>
    </>
  );
}
