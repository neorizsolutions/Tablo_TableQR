START TRANSACTION;

ALTER TABLE "TableBills" ADD "PaymentRevision" character varying(64);

ALTER TABLE "TableBills" ADD CONSTRAINT "AK_TableBills_RestaurantId_Id" UNIQUE ("RestaurantId", "Id");

CREATE TABLE "BillPayments" (
    "Id" uuid NOT NULL,
    "RestaurantId" uuid NOT NULL,
    "TableBillId" uuid NOT NULL,
    "ActorEmployeeId" uuid NOT NULL,
    "OperationId" uuid NOT NULL,
    "PayloadHash" character varying(64) NOT NULL,
    "Method" character varying(20) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "CollectedAtUtc" timestamp with time zone NOT NULL,
    "AcknowledgementJson" jsonb NOT NULL,
    CONSTRAINT "PK_BillPayments" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_BillPayments_PositiveAmount" CHECK ("Amount" > 0),
    CONSTRAINT "FK_BillPayments_Employees_RestaurantId_ActorEmployeeId" FOREIGN KEY ("RestaurantId", "ActorEmployeeId") REFERENCES "Employees" ("RestaurantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_BillPayments_TableBills_RestaurantId_TableBillId" FOREIGN KEY ("RestaurantId", "TableBillId") REFERENCES "TableBills" ("RestaurantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "DiningGroups" (
    "Id" uuid NOT NULL,
    "RestaurantId" uuid NOT NULL,
    "TableId" uuid NOT NULL,
    "Label" character varying(8) NOT NULL,
    "CreatedByEmployeeId" uuid NOT NULL,
    "CreationOperationId" uuid NOT NULL,
    "CreationHash" character varying(64) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ClosedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_DiningGroups" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_DiningGroups_RestaurantId_Id" UNIQUE ("RestaurantId", "Id"),
    CONSTRAINT "FK_DiningGroups_Employees_RestaurantId_CreatedByEmployeeId" FOREIGN KEY ("RestaurantId", "CreatedByEmployeeId") REFERENCES "Employees" ("RestaurantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DiningGroups_RestaurantTables_RestaurantId_TableId" FOREIGN KEY ("RestaurantId", "TableId") REFERENCES "RestaurantTables" ("RestaurantId", "Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_BillPayments_RestaurantId_ActorEmployeeId" ON "BillPayments" ("RestaurantId", "ActorEmployeeId");

CREATE INDEX "IX_BillPayments_RestaurantId_CollectedAtUtc" ON "BillPayments" ("RestaurantId", "CollectedAtUtc");

CREATE UNIQUE INDEX "IX_BillPayments_RestaurantId_OperationId" ON "BillPayments" ("RestaurantId", "OperationId");

CREATE INDEX "IX_BillPayments_RestaurantId_TableBillId" ON "BillPayments" ("RestaurantId", "TableBillId");

CREATE INDEX "IX_DiningGroups_RestaurantId_CreatedByEmployeeId" ON "DiningGroups" ("RestaurantId", "CreatedByEmployeeId");

CREATE UNIQUE INDEX "IX_DiningGroups_RestaurantId_CreationOperationId" ON "DiningGroups" ("RestaurantId", "CreationOperationId");

CREATE UNIQUE INDEX "IX_DiningGroups_RestaurantId_TableId_Label" ON "DiningGroups" ("RestaurantId", "TableId", "Label") WHERE "ClosedAtUtc" IS NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261116092356_AddDiningGroupsAndPaymentLedgerFoundation', '8.0.10');

COMMIT;

