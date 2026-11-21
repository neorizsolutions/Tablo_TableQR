import fs from 'node:fs';
import path from 'node:path';

const snapshotPath = path.resolve('Tablo.Api/Migrations/ApplicationDbContextModelSnapshot.cs');
let content = fs.readFileSync(snapshotPath, 'utf8');

// 1. Add MaxKitchenStations to Plan
if (!content.includes('"MaxKitchenStations"')) {
  const planTarget = 'b.Property<int>("MaxActiveTables")\n                        .HasColumnType("integer");';
  const planAddition = 'b.Property<int>("MaxActiveTables")\n                        .HasColumnType("integer");\n\n                    b.Property<int>("MaxKitchenStations")\n                        .ValueGeneratedOnAdd()\n                        .HasColumnType("integer")\n                        .HasDefaultValue(1);';
  content = content.replace(planTarget, planAddition);
}

// 2. Add KitchenStationId to Category
if (!content.includes('b.Property<Guid?>("KitchenStationId")')) {
  const catTarget = 'b.Property<DateTime>("CreatedAt")\n                        .ValueGeneratedOnAdd()';
  const catAddition = 'b.Property<Guid?>("KitchenStationId")\n                        .HasColumnType("uuid");\n\n                    b.Property<DateTime>("CreatedAt")\n                        .ValueGeneratedOnAdd()';
  content = content.replace(catTarget, catAddition);

  const catIndexTarget = 'b.HasIndex("RestaurantId");\n\n                    b.ToTable("Categories"';
  const catIndexAddition = 'b.HasIndex("KitchenStationId");\n\n                    b.HasIndex("RestaurantId");\n\n                    b.ToTable("Categories"';
  content = content.replace(catIndexTarget, catIndexAddition);
}

// 3. Add KitchenStationId and PackagingFee to MenuItem
if (!content.includes('b.Property<decimal?>("PackagingFee")')) {
  const miTarget = 'b.Property<decimal>("Price")\n                        .HasColumnType("decimal(18,2)");';
  const miAddition = 'b.Property<Guid?>("KitchenStationId")\n                        .HasColumnType("uuid");\n\n                    b.Property<decimal?>("PackagingFee")\n                        .ValueGeneratedOnAdd()\n                        .HasColumnType("decimal(18,2)")\n                        .HasDefaultValue(0m);\n\n                    b.Property<decimal>("Price")\n                        .HasColumnType("decimal(18,2)");';
  content = content.replace(miTarget, miAddition);

  const miIndexTarget = 'b.HasIndex("RestaurantId");\n\n                    b.ToTable("MenuItems"';
  const miIndexAddition = 'b.HasIndex("KitchenStationId");\n\n                    b.HasIndex("RestaurantId");\n\n                    b.ToTable("MenuItems"';
  content = content.replace(miIndexTarget, miIndexAddition);
}

// 4. Update Order: TableId nullable, add parcel/kot columns
if (!content.includes('"OrderChannel"')) {
  const orderTarget = 'b.Property<Guid>("TableId")\n                        .HasColumnType("uuid");';
  const orderReplacement = `b.Property<string>("BusinessDate")
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)");

                    b.Property<string>("CustomerName")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("CustomerPhone")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<int?>("DailyTokenNumber")
                        .HasColumnType("integer");

                    b.Property<string>("KotPrintError")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<string>("KotPrintStatus")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasDefaultValue("None");

                    b.Property<string>("OrderChannel")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)")
                        .HasDefaultValue("DineIn");

                    b.Property<decimal>("PackagingCharge")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("decimal(18,2)")
                        .HasDefaultValue(0m);

                    b.Property<string>("SubmissionOperationId")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<Guid?>("TableId")
                        .HasColumnType("uuid");`;
  content = content.replace(orderTarget, orderReplacement);

  const orderIndexTarget = 'b.HasIndex("RestaurantId", "TableBillId");';
  const orderIndexReplacement = `b.HasIndex("RestaurantId", "TableBillId");

                    b.HasIndex("RestaurantId", "BusinessDate", "DailyTokenNumber");

                    b.HasIndex("RestaurantId", "SubmissionOperationId")
                        .IsUnique()
                        .HasFilter("\\"SubmissionOperationId\\" IS NOT NULL");`;
  content = content.replace(orderIndexTarget, orderIndexReplacement);
}

// 5. Update TableBill: TableId nullable, add parcel/packaging columns
if (!content.includes('"PackagingChargeModeSnapshot"')) {
  const billTarget = 'b.Property<int>("TableNumberSnapshot")\n                        .HasColumnType("integer");';
  const billReplacement = `b.Property<int>("TableNumberSnapshot")
                        .HasColumnType("integer");

                    b.Property<string>("BusinessDate")
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)");

                    b.Property<int?>("DailyTokenNumber")
                        .HasColumnType("integer");

                    b.Property<bool>("IsPackagingWaived")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<string>("OrderChannel")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)")
                        .HasDefaultValue("DineIn");

                    b.Property<decimal>("PackagingCharge")
                        .ValueGeneratedOnAdd()
                        .HasPrecision(18, 2)
                        .HasColumnType("numeric(18,2)")
                        .HasDefaultValue(0m);

                    b.Property<string>("PackagingChargeModeSnapshot")
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)");

                    b.Property<decimal>("PackagingRateSnapshot")
                        .ValueGeneratedOnAdd()
                        .HasPrecision(18, 2)
                        .HasColumnType("numeric(18,2)")
                        .HasDefaultValue(0m);

                    b.Property<string>("PackagingWaivedBy")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("PackagingWaiverReason")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");`;
  content = content.replace(billTarget, billReplacement);

  // TableId nullable in TableBill
  content = content.replace('b.Property<Guid>("TableId")\n                        .HasColumnType("uuid");', 'b.Property<Guid?>("TableId")\n                        .HasColumnType("uuid");');

  // Update index filter on TableBills
  const oldFilter = '.HasFilter("\\"DiningGroupId\\" IS NULL AND \\"SettledAtUtc\\" IS NULL AND \\"SplitAtUtc\\" IS NULL AND \\"PaymentRevision\\" IS NULL AND \\"ParentBillId\\" IS NULL");';
  const newFilter = '.HasFilter("\\"TableId\\" IS NOT NULL AND \\"DiningGroupId\\" IS NULL AND \\"SettledAtUtc\\" IS NULL AND \\"SplitAtUtc\\" IS NULL AND \\"PaymentRevision\\" IS NULL AND \\"ParentBillId\\" IS NULL");';
  content = content.replaceAll(oldFilter, newFilter);

  const billIndexTarget = 'b.HasIndex("RestaurantId", "TableId", "DiningGroupId")';
  const billIndexReplacement = `b.HasIndex("RestaurantId", "BusinessDate", "DailyTokenNumber");\n\n                    b.HasIndex("RestaurantId", "TableId", "DiningGroupId")`;
  content = content.replace(billIndexTarget, billIndexReplacement);
}

// 6. Update RestaurantSettings
if (!content.includes('"KotPrintingEnabled"')) {
  const rsTarget = 'b.Property<bool>("AutoManageAvailability")\n                        .HasColumnType("boolean");';
  const rsReplacement = `b.Property<bool>("AutoManageAvailability")
                        .HasColumnType("boolean");

                    b.Property<Guid?>("DefaultKitchenStationId")
                        .HasColumnType("uuid");

                    b.Property<string>("KitchenSetupMode")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasDefaultValue("SingleKitchen");

                    b.Property<bool>("KotPrintingEnabled")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<string>("ParcelPackagingChargeMode")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)")
                        .HasDefaultValue("None");

                    b.Property<decimal>("ParcelPackagingFlatRate")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("decimal(18,2)")
                        .HasDefaultValue(0m);

                    b.Property<decimal>("ParcelPackagingPercentage")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("decimal(18,2)")
                        .HasDefaultValue(0m);

                    b.Property<bool>("ParcelPackagingTaxable")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<decimal?>("ParcelPackagingTaxRate")
                        .HasColumnType("decimal(18,2)");`;
  content = content.replace(rsTarget, rsReplacement);

  const rsIndexTarget = 'b.ToTable("RestaurantSettings", (string)null);\n                });';
  const rsIndexReplacement = `b.HasIndex("DefaultKitchenStationId");\n\n                    b.ToTable("RestaurantSettings", (string)null);\n                });`;
  content = content.replace(rsIndexTarget, rsIndexReplacement);
}

// 7. Add new entity definitions before relationship sections
const newEntities = `
            modelBuilder.Entity("Tablo.Api.Entities.DailyTokenSequence", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("BusinessDate")
                        .IsRequired()
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)");

                    b.Property<int>("LastTokenNumber")
                        .HasColumnType("integer");

                    b.Property<Guid>("RestaurantId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("UpdatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("RestaurantId", "BusinessDate")
                        .IsUnique();

                    b.ToTable("DailyTokenSequences");
                });

            modelBuilder.Entity("Tablo.Api.Entities.KitchenStation", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<DateTime>("CreatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<int>("DisplayOrder")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0);

                    b.Property<bool>("IsActive")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(true);

                    b.Property<bool>("IsDefault")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<Guid?>("OutletId")
                        .HasColumnType("uuid");

                    b.Property<Guid?>("PrinterId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("RestaurantId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("UpdatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("PrinterId");

                    b.HasIndex("RestaurantId", "Code")
                        .IsUnique();

                    b.HasIndex("RestaurantId", "DisplayOrder");

                    b.ToTable("KitchenStations");
                });

            modelBuilder.Entity("Tablo.Api.Entities.KotBatch", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("ActorEmail")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<int>("BatchSequence")
                        .HasColumnType("integer");

                    b.Property<DateTime>("CreatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("KotNumber")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<Guid>("OrderId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("RestaurantId")
                        .HasColumnType("uuid");

                    b.Property<string>("TriggeredBy")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.HasKey("Id");

                    b.HasIndex("OrderId");

                    b.HasIndex("RestaurantId", "KotNumber")
                        .IsUnique();

                    b.HasIndex("RestaurantId", "OrderId", "BatchSequence")
                        .IsUnique();

                    b.ToTable("KotBatches");
                });

            modelBuilder.Entity("Tablo.Api.Entities.KotTicket", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid?>("DiningGroupId")
                        .HasColumnType("uuid");

                    b.Property<string>("GroupLabelSnapshot")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<bool>("IsReprint")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<string>("ItemsJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("KitchenStationId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("KotBatchId")
                        .HasColumnType("uuid");

                    b.Property<string>("Notes")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<Guid>("OrderId")
                        .HasColumnType("uuid");

                    b.Property<int>("OrderNumber")
                        .HasColumnType("integer");

                    b.Property<Guid?>("OriginalTicketId")
                        .HasColumnType("uuid");

                    b.Property<Guid?>("PrinterId")
                        .HasColumnType("uuid");

                    b.Property<int>("ReprintCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0);

                    b.Property<string>("ReprintReason")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("ReprinterEmail")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<Guid>("RestaurantId")
                        .HasColumnType("uuid");

                    b.Property<string>("StationNameSnapshot")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<Guid?>("TableId")
                        .HasColumnType("uuid");

                    b.Property<int>("TableNumber")
                        .HasColumnType("integer");

                    b.Property<string>("TicketNumber")
                        .IsRequired()
                        .HasMaxLength(60)
                        .HasColumnType("character varying(60)");

                    b.Property<string>("TicketType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("TimestampIstFormatted")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<DateTime>("TimestampUtc")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("KitchenStationId");

                    b.HasIndex("KotBatchId");

                    b.HasIndex("OriginalTicketId");

                    b.HasIndex("PrinterId");

                    b.HasIndex("RestaurantId", "OrderId");

                    b.HasIndex("RestaurantId", "KitchenStationId", "CreatedAtUtc");

                    b.ToTable("KotTickets");
                });

            modelBuilder.Entity("Tablo.Api.Entities.PrintAgentRegistration", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AgentName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("ApiKeyHash")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<DateTime>("CreatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("IsActive")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(true);

                    b.Property<string>("KeyPrefix")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)");

                    b.Property<DateTime?>("LastHeartbeatUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid?>("OutletId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("RestaurantId")
                        .HasColumnType("uuid");

                    b.Property<DateTime?>("RevokedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Version")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.HasKey("Id");

                    b.HasIndex("ApiKeyHash")
                        .IsUnique();

                    b.HasIndex("RestaurantId", "IsActive");

                    b.ToTable("PrintAgentRegistrations");
                });

            modelBuilder.Entity("Tablo.Api.Entities.PrintJob", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AttemptHistoryJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Attempts")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("ClaimedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid?>("ClaimedByAgentId")
                        .HasColumnType("uuid");

                    b.Property<string>("ClaimToken")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<DateTime?>("CompletedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("ConcurrencyToken")
                        .IsConcurrencyToken()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("CreatedBy")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("DeliveryState")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)")
                        .HasDefaultValue("Certain");

                    b.Property<string>("JobType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<Guid?>("KotTicketId")
                        .HasColumnType("uuid");

                    b.Property<DateTime?>("LastAttemptUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("LastError")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<DateTime?>("LeaseExpiresAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("MaxAttempts")
                        .HasColumnType("integer");

                    b.Property<int>("PaperWidthMm")
                        .HasColumnType("integer");

                    b.Property<string>("PayloadEscPosBase64")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("PrinterId")
                        .HasColumnType("uuid");

                    b.Property<string>("ReadableText")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("RestaurantId")
                        .HasColumnType("uuid");

                    b.Property<Guid?>("StationId")
                        .HasColumnType("uuid");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("UncertainReason")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<DateTime>("UpdatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("KotTicketId");

                    b.HasIndex("StationId");

                    b.HasIndex("PrinterId", "Status", "CreatedAtUtc");

                    b.HasIndex("RestaurantId", "PrinterId", "Status");

                    b.HasIndex("RestaurantId", "Status", "LeaseExpiresAtUtc");

                    b.ToTable("PrintJobs");
                });

            modelBuilder.Entity("Tablo.Api.Entities.ThermalPrinter", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int>("CharactersPerLine")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(48);

                    b.Property<string>("ConnectionType")
                        .IsRequired()
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)");

                    b.Property<DateTime>("CreatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("IpAddress")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<bool>("IsActive")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(true);

                    b.Property<DateTime?>("LastSeenAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("LastStatusMessage")
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<string>("LocalQueueName")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<Guid?>("OutletId")
                        .HasColumnType("uuid");

                    b.Property<int>("PaperWidthMm")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(80);

                    b.Property<int>("Port")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(9100);

                    b.Property<Guid>("RestaurantId")
                        .HasColumnType("uuid");

                    b.Property<string>("Status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)")
                        .HasDefaultValue("Online");

                    b.Property<DateTime>("UpdatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("RestaurantId", "IsActive");

                    b.HasIndex("RestaurantId", "Name");

                    b.ToTable("ThermalPrinters");
                });
`;

if (!content.includes('modelBuilder.Entity("Tablo.Api.Entities.KitchenStation"')) {
  const insertEntityMarker = 'modelBuilder.Entity("Tablo.Api.Entities.BillPayment", b =>\n                {\n                    b.HasOne(';
  content = content.replace(insertEntityMarker, `${newEntities}\n            ${insertEntityMarker}`);
}

// 8. Add foreign key relationships for new entities
const newRelationships = `
            modelBuilder.Entity("Tablo.Api.Entities.Category", b =>
                {
                    b.HasOne("Tablo.Api.Entities.KitchenStation", "KitchenStation")
                        .WithMany()
                        .HasForeignKey("KitchenStationId")
                        .OnDelete(DeleteBehavior.SetNull);
                });

            modelBuilder.Entity("Tablo.Api.Entities.DailyTokenSequence", b =>
                {
                    b.HasOne("Tablo.Api.Entities.Restaurant", "Restaurant")
                        .WithMany()
                        .HasForeignKey("RestaurantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Restaurant");
                });

            modelBuilder.Entity("Tablo.Api.Entities.KitchenStation", b =>
                {
                    b.HasOne("Tablo.Api.Entities.ThermalPrinter", "Printer")
                        .WithMany("Stations")
                        .HasForeignKey("PrinterId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("Tablo.Api.Entities.Restaurant", "Restaurant")
                        .WithMany()
                        .HasForeignKey("RestaurantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Printer");

                    b.Navigation("Restaurant");
                });

            modelBuilder.Entity("Tablo.Api.Entities.KotBatch", b =>
                {
                    b.HasOne("Tablo.Api.Entities.Order", "Order")
                        .WithMany()
                        .HasForeignKey("OrderId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Tablo.Api.Entities.Restaurant", "Restaurant")
                        .WithMany()
                        .HasForeignKey("RestaurantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Order");

                    b.Navigation("Restaurant");
                });

            modelBuilder.Entity("Tablo.Api.Entities.KotTicket", b =>
                {
                    b.HasOne("Tablo.Api.Entities.KitchenStation", "KitchenStation")
                        .WithMany("KotTickets")
                        .HasForeignKey("KitchenStationId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("Tablo.Api.Entities.KotBatch", "KotBatch")
                        .WithMany("Tickets")
                        .HasForeignKey("KotBatchId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Tablo.Api.Entities.KotTicket", "OriginalTicket")
                        .WithMany()
                        .HasForeignKey("OriginalTicketId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("Tablo.Api.Entities.Order", "Order")
                        .WithMany()
                        .HasForeignKey("OrderId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Tablo.Api.Entities.ThermalPrinter", "Printer")
                        .WithMany()
                        .HasForeignKey("PrinterId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("Tablo.Api.Entities.Restaurant", "Restaurant")
                        .WithMany()
                        .HasForeignKey("RestaurantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("KitchenStation");

                    b.Navigation("KotBatch");

                    b.Navigation("Order");

                    b.Navigation("Restaurant");
                });

            modelBuilder.Entity("Tablo.Api.Entities.MenuItem", b =>
                {
                    b.HasOne("Tablo.Api.Entities.KitchenStation", "KitchenStation")
                        .WithMany("MenuItems")
                        .HasForeignKey("KitchenStationId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.Navigation("KitchenStation");
                });

            modelBuilder.Entity("Tablo.Api.Entities.PrintAgentRegistration", b =>
                {
                    b.HasOne("Tablo.Api.Entities.Restaurant", "Restaurant")
                        .WithMany()
                        .HasForeignKey("RestaurantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Restaurant");
                });

            modelBuilder.Entity("Tablo.Api.Entities.PrintJob", b =>
                {
                    b.HasOne("Tablo.Api.Entities.KotTicket", "KotTicket")
                        .WithMany("PrintJobs")
                        .HasForeignKey("KotTicketId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("Tablo.Api.Entities.ThermalPrinter", "Printer")
                        .WithMany("PrintJobs")
                        .HasForeignKey("PrinterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Tablo.Api.Entities.Restaurant", "Restaurant")
                        .WithMany()
                        .HasForeignKey("RestaurantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Tablo.Api.Entities.KitchenStation", "Station")
                        .WithMany()
                        .HasForeignKey("StationId");

                    b.Navigation("KotTicket");

                    b.Navigation("Printer");

                    b.Navigation("Restaurant");

                    b.Navigation("Station");
                });

            modelBuilder.Entity("Tablo.Api.Entities.RestaurantSettings", b =>
                {
                    b.HasOne("Tablo.Api.Entities.KitchenStation", "DefaultKitchenStation")
                        .WithMany()
                        .HasForeignKey("DefaultKitchenStationId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.Navigation("DefaultKitchenStation");
                });

            modelBuilder.Entity("Tablo.Api.Entities.ThermalPrinter", b =>
                {
                    b.HasOne("Tablo.Api.Entities.Restaurant", "Restaurant")
                        .WithMany()
                        .HasForeignKey("RestaurantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Restaurant");
                });
`;

if (!content.includes('modelBuilder.Entity("Tablo.Api.Entities.DailyTokenSequence", b =>\n                {\n                    b.HasOne')) {
  const insertRelMarker = 'modelBuilder.Entity("Tablo.Api.Entities.CustomerTableOrder", b =>';
  if (content.includes(insertRelMarker)) {
    content = content.replace(insertRelMarker, `${newRelationships}\n            ${insertRelMarker}`);
  } else {
    const endMarker = 'modelBuilder.Entity("Tablo.Api.Entities.TableBill", b =>\n                {\n                    b.Navigation("Orders");\n                });';
    content = content.replace(endMarker, `${endMarker}\n${newRelationships}`);
  }
}

// 9. Add navigations on KitchenStation, ThermalPrinter, KotBatch, KotTicket
const newNavigations = `
            modelBuilder.Entity("Tablo.Api.Entities.KitchenStation", b =>
                {
                    b.Navigation("KotTickets");

                    b.Navigation("MenuItems");
                });

            modelBuilder.Entity("Tablo.Api.Entities.KotBatch", b =>
                {
                    b.Navigation("Tickets");
                });

            modelBuilder.Entity("Tablo.Api.Entities.KotTicket", b =>
                {
                    b.Navigation("PrintJobs");
                });

            modelBuilder.Entity("Tablo.Api.Entities.ThermalPrinter", b =>
                {
                    b.Navigation("PrintJobs");

                    b.Navigation("Stations");
                });
`;

if (!content.includes('modelBuilder.Entity("Tablo.Api.Entities.KitchenStation", b =>\n                {\n                    b.Navigation("KotTickets")')) {
  const navEndMarker = 'modelBuilder.Entity("Tablo.Api.Entities.TableBill", b =>\n                {\n                    b.Navigation("Orders");\n                });';
  content = content.replace(navEndMarker, `${navEndMarker}\n${newNavigations}`);
}

fs.writeFileSync(snapshotPath, content, 'utf8');
console.log('Updated ApplicationDbContextModelSnapshot.cs successfully.');

// Now produce 20261119170000_AddThermalPrintingAndParcelCounter.Designer.cs
let designerContent = content.replace(
  'using Microsoft.EntityFrameworkCore.Infrastructure;',
  'using Microsoft.EntityFrameworkCore.Infrastructure;\nusing Microsoft.EntityFrameworkCore.Migrations;'
).replace(
  /partial class ApplicationDbContextModelSnapshot : ModelSnapshot[\r\n\s]+{[\r\n\s]+protected override void BuildModel\(ModelBuilder modelBuilder\)/,
  `[Migration("20261119170000_AddThermalPrintingAndParcelCounter")]
    partial class AddThermalPrintingAndParcelCounter
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)`
);

fs.writeFileSync(path.resolve('Tablo.Api/Migrations/20261119170000_AddThermalPrintingAndParcelCounter.Designer.cs'), designerContent, 'utf8');
console.log('Generated 20261119170000_AddThermalPrintingAndParcelCounter.Designer.cs successfully.');
