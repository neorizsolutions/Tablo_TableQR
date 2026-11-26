import { test } from "node:test";
import assert from "node:assert/strict";
import {
  NAVIGATION_REGISTRY,
  SUPER_ADMIN_NAVIGATION,
  getEmployeeNavItems,
  getRouteMenuItem,
} from "../src/config/navigation.ts";
import {
  applyRealtimeEventToDetail,
  applyRealtimeEventToList,
  mergeMessages,
  mergeSyncResponse,
  upsertConversationSummary,
} from "../src/lib/support-state.ts";

test("Support menu item is restricted to Admin/Owner and preserves role defaults", () => {
  const supportItem = NAVIGATION_REGISTRY.find((x) => x.id === "support");
  assert.ok(supportItem);
  assert.equal(supportItem.to, "/support");
  assert.deepEqual(supportItem.requiredRoles, ["Admin", "Owner"]);
  assert.equal(supportItem.requiredCapabilities, undefined);

  assert.equal(getRouteMenuItem("/support", undefined, "Admin")?.id, "support");
  assert.ok(getEmployeeNavItems("Admin", null).some((x) => x.id === "support"));
  assert.ok(getEmployeeNavItems("Owner", null).some((x) => x.id === "support"));
  assert.ok(!getEmployeeNavItems("Waiter", null).some((x) => x.id === "support"));
  assert.ok(!getEmployeeNavItems("Kitchen", null).some((x) => x.id === "support"));
  assert.ok(!getEmployeeNavItems("Cashier", null).some((x) => x.id === "support"));

  // Custom override omitting "support" restricts Admin access cleanly while keeping Cashier untouched
  assert.ok(!getEmployeeNavItems("Admin", ["staff-management"]).some((x) => x.id === "support"));
  assert.deepEqual(
    getEmployeeNavItems("Cashier", null).map((x) => x.id),
    ["cashier-billing"],
  );
});

test("Super Admin navigation includes Support Inbox tab", () => {
  const inbox = SUPER_ADMIN_NAVIGATION.find((x) => x.id === "sa-support-inbox");
  assert.ok(inbox);
  assert.equal(inbox.tab, "inbox");
  assert.equal(inbox.to, "/super-admin");
  assert.equal(inbox.search.tab, "inbox");
});

test("mergeMessages deduplicates repeated events and reconciles optimistic messages by clientOperationId", () => {
  const opId = "11111111-1111-4111-8111-111111111111";
  const optimistic = [
    {
      id: `opt-${opId}`,
      conversationId: "conv-1",
      senderType: "Restaurant",
      senderName: "Admin",
      senderEmail: "admin@test.com",
      message: "Printer jam on station 1",
      createdAtUtc: "2026-11-25T10:00:05.000Z",
      sequenceNumber: Number.MAX_SAFE_INTEGER,
      clientOperationId: opId,
      localStatus: "sending",
    },
  ];

  const confirmed = {
    id: "msg-server-1",
    conversationId: "conv-1",
    senderType: "Restaurant",
    senderName: "Admin",
    senderEmail: "admin@test.com",
    message: "Printer jam on station 1",
    createdAtUtc: "2026-11-25T10:00:01.000Z",
    sequenceNumber: 1,
    clientOperationId: opId,
  };

  // Duplicate server push + HTTP response reconciliation must yield 1 message without optimistic leftover
  const merged = mergeMessages(optimistic, [confirmed, confirmed]);
  assert.equal(merged.length, 1);
  assert.equal(merged[0].id, "msg-server-1");
  assert.equal(merged[0].localStatus, undefined);
});

test("upsertConversationSummary prevents stale version regression and prioritizes escalated superadmin items", () => {
  const convA = {
    id: "conv-a",
    restaurantId: "rest-a",
    restaurantName: "Alpha Bistro",
    subject: "Alpha ticket",
    status: "AwaitingSupport",
    createdAtUtc: "2026-11-25T10:00:00Z",
    updatedAtUtc: "2026-11-25T10:15:00Z",
    lastActivityAtUtc: "2026-11-25T10:05:00Z",
    isEscalated: true,
    restaurantUnreadCount: 0,
    superAdminUnreadCount: 2,
    version: 3,
  };
  const convB = {
    id: "conv-b",
    restaurantId: "rest-b",
    restaurantName: "Beta Cafe",
    subject: "Beta ticket",
    status: "AwaitingSupport",
    createdAtUtc: "2026-11-25T10:10:00Z",
    updatedAtUtc: "2026-11-25T10:12:00Z",
    lastActivityAtUtc: "2026-11-25T10:12:00Z",
    isEscalated: false,
    restaurantUnreadCount: 0,
    superAdminUnreadCount: 1,
    version: 1,
  };

  let list = upsertConversationSummary([], convB, "superadmin");
  list = upsertConversationSummary(list, convA, "superadmin");
  assert.equal(list[0].id, "conv-a"); // Escalated sorts ahead of newer unescalated

  // Out-of-order stale update with lower version must be ignored
  const stale = { ...convA, version: 2, subject: "Stale Subject" };
  list = upsertConversationSummary(list, stale, "superadmin");
  assert.equal(list[0].subject, "Alpha ticket");
  assert.equal(list[0].version, 3);
});

test("Realtime list and detail cache updaters synchronize unread counts, replies, and delta recovery", () => {
  const initialSummary = {
    id: "conv-1",
    restaurantId: "rest-1",
    restaurantName: "Hotel City",
    subject: "Billing question",
    status: "AwaitingSupport",
    createdAtUtc: "2026-11-25T10:00:00Z",
    updatedAtUtc: "2026-11-25T10:00:00Z",
    lastActivityAtUtc: "2026-11-25T10:00:00Z",
    isEscalated: false,
    restaurantUnreadCount: 0,
    superAdminUnreadCount: 1,
    version: 1,
  };

  let restList = mergeSyncResponse(
    undefined,
    { serverUtc: "2026-11-25T10:00:00Z", conversations: [initialSummary], totalUnreadCount: 0 },
    "restaurant",
    true,
  );
  let restDetail = {
    conversation: initialSummary,
    messages: [
      {
        id: "m1",
        conversationId: "conv-1",
        senderType: "Restaurant",
        senderName: "City Admin",
        senderEmail: "city@admin.com",
        message: "Need help with GST",
        createdAtUtc: "2026-11-25T10:00:00Z",
        sequenceNumber: 1,
      },
    ],
    escalationLogs: [],
    auditLogs: [],
  };

  const replyEvent = {
    eventId: "evt-2",
    eventType: "MessageCreated",
    occurredAtUtc: "2026-11-25T10:02:00Z",
    conversationId: "conv-1",
    restaurantId: "rest-1",
    conversationVersion: 2,
    conversation: {
      ...initialSummary,
      status: "AwaitingRestaurant",
      updatedAtUtc: "2026-11-25T10:02:00Z",
      lastActivityAtUtc: "2026-11-25T10:02:00Z",
      restaurantUnreadCount: 1,
      superAdminUnreadCount: 0,
      lastMessagePreview: "We checked your GST rate",
      version: 2,
    },
    message: {
      id: "m2",
      conversationId: "conv-1",
      senderType: "SuperAdmin",
      senderName: "Tablo Support",
      senderEmail: "superadmin@tablo.local",
      message: "We checked your GST rate",
      createdAtUtc: "2026-11-25T10:02:00Z",
      sequenceNumber: 2,
    },
  };

  restList = applyRealtimeEventToList(restList, replyEvent, "restaurant");
  restDetail = applyRealtimeEventToDetail(restDetail, replyEvent);

  assert.equal(restList.totalUnreadCount, 1);
  assert.equal(restList.conversations[0].status, "AwaitingRestaurant");
  assert.equal(restDetail.messages.length, 2);
  assert.equal(restDetail.messages[1].sequenceNumber, 2);
});
