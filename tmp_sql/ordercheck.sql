SELECT oi."Id", oi."OrderId", oi."ItemNameSnapshot", oi."UnitPrice", oi."Quantity",
       o."RestaurantId", o."TableId", o."Status", o."OrderNumber"
FROM "OrderItems" oi
JOIN "Orders" o ON oi."OrderId" = o."Id"
ORDER BY o."CreatedAt" DESC
LIMIT 10;
