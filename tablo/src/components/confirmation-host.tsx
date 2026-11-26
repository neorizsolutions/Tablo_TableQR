import { useSyncExternalStore } from "react";
import * as AlertDialog from "@radix-ui/react-alert-dialog";
import { subscribeConfirmation, getConfirmation, finishConfirmation } from "@/lib/confirmation";

export function ConfirmationHost() {
  const request = useSyncExternalStore(subscribeConfirmation, getConfirmation, () => null);
  return (
    <AlertDialog.Root open={!!request} onOpenChange={open => { if (!open) finishConfirmation(false); }}>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="fixed inset-0 z-[200] bg-black/50 backdrop-blur-sm" />
        <AlertDialog.Content className="fixed left-1/2 top-1/2 z-[201] w-[calc(100%_-_2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-2xl border border-border bg-card p-6 shadow-xl">
          <AlertDialog.Title className="text-xl font-bold">{request?.title}</AlertDialog.Title>
          <AlertDialog.Description className="mt-3 text-sm text-muted-foreground">{request?.description}</AlertDialog.Description>
          <div className="mt-6 flex justify-end gap-3">
            {!request?.notice && <AlertDialog.Cancel asChild><button type="button" className="rounded-full border px-4 py-2 text-sm font-semibold" onClick={event => { event.preventDefault(); finishConfirmation(false); }}>Cancel</button></AlertDialog.Cancel>}
            <AlertDialog.Action asChild><button type="button" className={`rounded-full px-4 py-2 text-sm font-semibold text-white ${request?.destructive ? "bg-destructive" : "bg-primary"}`} onClick={event => { event.preventDefault(); finishConfirmation(true); }}>{request?.confirmLabel ?? "Confirm"}</button></AlertDialog.Action>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}
