import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { toast } from "sonner";
import { API_BASE_URL } from "@/services/apiClient";
import { getServiceTables } from "@/services/service-desk";
import { exportPayments, getPaymentReceipt, getPaymentReport } from "@/services/payment-reports";
import { formatINR } from "@/lib/mock-data";
import { PaymentReceipt } from "./payment-receipt";

const control = "rounded-lg border border-border bg-background px-3 py-2 text-sm";
export function PaymentReports({ restaurantId }: { restaurantId: string }) {
  const [startDate, setStart] = useState("");
  const [endDate, setEnd] = useState("");
  const [tableId, setTable] = useState("");
  const [paymentMethod, setMethod] = useState("");
  const [page, setPage] = useState(1);
  const [exporting, setExporting] = useState(false);
  const [receiptId, setReceipt] = useState("");
  const cache = useQueryClient();
  const filters = { startDate, endDate, tableId, paymentMethod, page, pageSize: 20 };
  const invalid = !!startDate && !!endDate && startDate > endDate;
  const report = useQuery({
    queryKey: ["paymentReports", restaurantId, filters],
    queryFn: () => getPaymentReport(restaurantId, filters),
    enabled: !invalid,
    refetchInterval: 15000,
  });
  const tables = useQuery({
    queryKey: ["serviceTables", restaurantId],
    queryFn: () => getServiceTables(restaurantId),
  });
  const receipt = useQuery({
    queryKey: ["paymentReceipt", restaurantId, receiptId],
    queryFn: () => getPaymentReceipt(restaurantId, receiptId),
    enabled: !!receiptId,
  });
  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(API_BASE_URL.replace(/\/api\/?$/, "/hubs/order"), {
        accessTokenFactory: () => localStorage.getItem("tablo_token") || "",
      })
      .withAutomaticReconnect()
      .build();
    let disposed = false;
    const refresh = () => {
      void cache.invalidateQueries({ queryKey: ["paymentReports", restaurantId] });
    };
    const join = async () => {
      await connection.invoke("JoinRestaurantGroup");
      refresh();
    };
    connection.on("BillUpdated", refresh);
    connection.onreconnected(() => join().catch(() => undefined));
    void connection
      .start()
      .then(() => {
        if (!disposed) return join();
      })
      .catch(() => undefined);
    return () => {
      disposed = true;
      void connection.stop();
    };
  }, [cache, restaurantId]);
  const data = report.data;
  return (
    <section
      aria-labelledby="payment-report-title"
      className="mb-8 rounded-3xl border border-border bg-card p-5 shadow-soft"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 id="payment-report-title" className="font-display text-2xl font-bold">
            Payment collections
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Settled bills only · settlement dates in Asia/Kolkata (IST). Payments are recorded as
            collected externally.
          </p>
        </div>
        <div className="flex gap-2">
          <button
            className={control}
            onClick={() => void report.refetch()}
            disabled={invalid || report.isFetching}
          >
            Refresh payments
          </button>
          <button
            className={control}
            disabled={invalid || exporting || !data?.totalCount}
            onClick={async () => {
              setExporting(true);
              try {
                await exportPayments(restaurantId, filters);
              } catch (e) {
                toast.error(e instanceof Error ? e.message : "Payment export failed.");
              } finally {
                setExporting(false);
              }
            }}
          >
            {exporting ? "Exporting…" : "Export payments"}
          </button>
        </div>
      </div>
      <div className="my-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <label className="grid gap-1 text-sm">
          Payment start date (IST)
          <input
            className={control}
            type="date"
            value={startDate}
            onChange={(e) => {
              setStart(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <label className="grid gap-1 text-sm">
          Payment end date (IST)
          <input
            className={control}
            type="date"
            value={endDate}
            onChange={(e) => {
              setEnd(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <label className="grid gap-1 text-sm">
          Payment table
          <select
            className={control}
            value={tableId}
            onChange={(e) => {
              setTable(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All tables</option>
            {tables.data?.map((t) => (
              <option key={t.id} value={t.id}>
                Table {t.tableNumber}
              </option>
            ))}
          </select>
        </label>
        <label className="grid gap-1 text-sm">
          Payment method
          <select
            className={control}
            value={paymentMethod}
            onChange={(e) => {
              setMethod(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All methods</option>
            {["Cash", "UPI", "Card"].map((m) => (
              <option key={m}>{m}</option>
            ))}
          </select>
        </label>
      </div>
      {invalid && <p role="alert">Start date cannot be after end date.</p>}
      {tables.isError && <p role="alert">Table filters could not load. Refresh to retry.</p>}
      {report.isError && (
        <p role="alert" className="text-destructive">
          {report.error.message}
        </p>
      )}
      {report.isPending && !invalid && <p role="status">Loading payment collections…</p>}
      {!invalid && data && !report.isError && (
        <>
          <dl className="mb-5 grid grid-cols-2 gap-3 lg:grid-cols-5">
            {[
              ["Collected", formatINR(data.totalCollected)],
              ["Settled bills", data.totalCount],
              ["Cash", formatINR(data.cash)],
              ["UPI", formatINR(data.upi)],
              ["Card", formatINR(data.card)],
            ].map(([name, value]) => (
              <div key={name} className="rounded-xl bg-secondary/50 p-3">
                <dt className="text-sm text-muted-foreground">{name}</dt>
                <dd className="text-xl font-bold">{value}</dd>
              </div>
            ))}
          </dl>
          {!data.totalCount ? (
            <p>No settled bills match these filters.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr>
                    {[
                      "Receipt",
                      "Table",
                      "Settled (IST)",
                      "Method",
                      "Collected",
                      "Receipt details",
                    ].map((h) => (
                      <th key={h} className="p-2">
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((row) => (
                    <tr key={row.id} className="border-t">
                      <td className="max-w-40 break-all p-2">{row.id}</td>
                      <td className="p-2">{row.tableNumber}</td>
                      <td className="p-2">
                        {new Date(row.settledAtUtc).toLocaleString("en-IN", {
                          timeZone: "Asia/Kolkata",
                        })}
                      </td>
                      <td className="p-2">{row.paymentMethod}</td>
                      <td className="p-2">{formatINR(row.totalAmount)}</td>
                      <td className="p-2">
                        <button className={control} onClick={() => setReceipt(row.id)}>
                          Show receipt
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {!!data.totalCount && (
            <div className="mt-4 flex items-center gap-3">
              <button
                className={control}
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
              >
                Previous payment page
              </button>
              <span>
                Page {page} of {data.totalPages}
              </span>
              <button
                className={control}
                disabled={page >= data.totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next payment page
              </button>
            </div>
          )}
        </>
      )}
      {receiptId && (
        <div className="mt-4 rounded-xl border p-4">
          <h3 className="font-semibold">Selected receipt</h3>
          {receipt.isPending && <p>Loading receipt…</p>}
          {receipt.isError && <p role="alert">{receipt.error.message}</p>}
          {receipt.data && (
            <>
              <p className="break-all text-sm">
                {receipt.data.id} · Recorded employee{" "}
                {receipt.data.settledByEmployeeId || "unavailable in legacy snapshot"}
              </p>
              <PaymentReceipt bill={receipt.data} />
            </>
          )}
          <button className={`${control} ml-2`} onClick={() => setReceipt("")}>
            Hide receipt
          </button>
        </div>
      )}
    </section>
  );
}
