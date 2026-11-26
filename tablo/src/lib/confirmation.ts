export type ConfirmationOptions = {
  title: string;
  description: string;
  confirmLabel?: string;
  destructive?: boolean;
  notice?: boolean;
};
type Request = ConfirmationOptions & { resolve: (accepted: boolean) => void };
const queue: Request[] = [];
const listeners = new Set<() => void>();
const emit = () => listeners.forEach(listener => listener());
export const subscribeConfirmation = (listener: () => void) => {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
};
export const getConfirmation = () => queue[0] ?? null;
export function finishConfirmation(accepted: boolean) {
  const request = queue.shift();
  request?.resolve(accepted);
  emit();
}
export function confirmAction(options: ConfirmationOptions | string): Promise<boolean> {
  return new Promise(resolve => {
    queue.push({ ...(typeof options === "string" ? {
      title: "Confirm action", description: options, destructive: true,
    } : options), resolve });
    emit();
  });
}
export async function confirmClose(close: () => void) {
  if (await confirmAction({ title: "Close this window?", description: "Any unsaved changes will be lost.", confirmLabel: "Close window" })) close();
}
export function showNotice(description: string) {
  return confirmAction({ title: "Notice", description, confirmLabel: "OK", notice: true });
}
