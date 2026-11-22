import type { QueryClient } from "@tanstack/react-query";
import type { HubConnection } from "@microsoft/signalr";

/** Status messages are invalidation signals, never authoritative order snapshots. */
export function subscribeCustomerOrder(
  connection: Pick<HubConnection, "on" | "onreconnected" | "invoke">,
  queryClient: QueryClient,
  orderId: string,
) {
  const refresh = () => queryClient.invalidateQueries({ queryKey: ["order", orderId] });
  connection.on("OrderStatusUpdated", async (updatedOrderId: string) => {
    if (
      typeof updatedOrderId === "string" &&
      updatedOrderId.toLowerCase() === orderId.toLowerCase()
    ) {
      await refresh();
    }
  });
  connection.onreconnected(async () => {
    try {
      await connection.invoke("JoinOrderGroup", orderId);
    } finally {
      await refresh();
    }
  });
  return refresh;
}
