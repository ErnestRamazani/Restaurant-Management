using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EliteRestaurant.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceDayValidations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDayValidations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniqueId = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    PrimaryPhone = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PreferredContactChannel = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    NoShowCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedReservationCount = table.Column<int>(type: "integer", nullable: false),
                    LastVisitAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniqueId = table.Column<string>(type: "text", nullable: false),
                    SignInId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    PinCode = table.Column<string>(type: "text", nullable: false),
                    ProfileImagePath = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric", nullable: false),
                    MonthlySalaryUSD = table.Column<decimal>(type: "numeric", nullable: false),
                    JoinDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmploymentStatus = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    MondayShift = table.Column<string>(type: "text", nullable: false),
                    TuesdayShift = table.Column<string>(type: "text", nullable: false),
                    WednesdayShift = table.Column<string>(type: "text", nullable: false),
                    ThursdayShift = table.Column<string>(type: "text", nullable: false),
                    FridayShift = table.Column<string>(type: "text", nullable: false),
                    SaturdayShift = table.Column<string>(type: "text", nullable: false),
                    SundayShift = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniqueId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    StockQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniqueId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    SubCategory = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SharedOrderDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniqueId = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: false),
                    Portal = table.Column<string>(type: "text", nullable: false),
                    DraftLabel = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedOrderDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountFc = table.Column<decimal>(type: "numeric", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    ExchangeRateUsed = table.Column<decimal>(type: "numeric", nullable: false),
                    Justification = table.Column<string>(type: "text", nullable: false),
                    IsFixed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaitlistEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniqueId = table.Column<string>(type: "text", nullable: false),
                    GuestName = table.Column<string>(type: "text", nullable: false),
                    GuestPhone = table.Column<string>(type: "text", nullable: false),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    QuotedWaitMinutes = table.Column<int>(type: "integer", nullable: true),
                    UserNotes = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeAttendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    WorkDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClockInTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClockOutTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClockInStatus = table.Column<string>(type: "text", nullable: false),
                    Justification = table.Column<string>(type: "text", nullable: false),
                    IsAbsence = table.Column<bool>(type: "boolean", nullable: false),
                    AbsenceJustification = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeAttendances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPaymentRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    MonthlySalaryUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    AbsenceDays = table.Column<int>(type: "integer", nullable: false),
                    LateDays = table.Column<int>(type: "integer", nullable: false),
                    LatePenaltyUnits = table.Column<int>(type: "integer", nullable: false),
                    TotalDeductionUnits = table.Column<int>(type: "integer", nullable: false),
                    MoneyGeneratedUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    BonusFivePercentUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    AdvancesDeductedUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    NetPayUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPaymentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPaymentRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalaryAdvances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    AmountUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    GivenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ForPayrollYear = table.Column<int>(type: "integer", nullable: true),
                    ForPayrollMonth = table.Column<int>(type: "integer", nullable: true),
                    AppliedPayrollYear = table.Column<int>(type: "integer", nullable: true),
                    AppliedPayrollMonth = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryAdvances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryAdvances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniqueId = table.Column<string>(type: "text", nullable: false),
                    TableNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AssignedServerId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tables_Employees_AssignedServerId",
                        column: x => x.AssignedServerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TabletSessions",
                columns: table => new
                {
                    Token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Portal = table.Column<string>(type: "text", nullable: false),
                    EmployeeUniqueId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    SignInId = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TabletSessions", x => x.Token);
                    table.ForeignKey(
                        name: "FK_TabletSessions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductIngredients_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductIngredients_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniqueId = table.Column<string>(type: "text", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: true),
                    TableCode = table.Column<string>(type: "text", nullable: false),
                    TableName = table.Column<string>(type: "text", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: true),
                    ServerName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CustomerNotes = table.Column<string>(type: "text", nullable: false),
                    AllergyNotes = table.Column<string>(type: "text", nullable: false),
                    DiscountMode = table.Column<string>(type: "text", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscountAmountUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentCurrencyCode = table.Column<string>(type: "text", nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentAmountUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentAmountFc = table.Column<decimal>(type: "numeric", nullable: false),
                    CustomerPaidUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    CustomerPaidFc = table.Column<decimal>(type: "numeric", nullable: false),
                    ChangeGivenUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    ChangeGivenFc = table.Column<decimal>(type: "numeric", nullable: false),
                    ExchangeRateUsed = table.Column<decimal>(type: "numeric", nullable: false),
                    OrderSource = table.Column<string>(type: "text", nullable: false),
                    ReservationBookingId = table.Column<int>(type: "integer", nullable: true),
                    ReservationCode = table.Column<string>(type: "text", nullable: false),
                    ReservationGuestName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Employees_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniqueId = table.Column<string>(type: "text", nullable: false),
                    ReservationName = table.Column<string>(type: "text", nullable: false),
                    CustomerProfileId = table.Column<int>(type: "integer", nullable: true),
                    GuestName = table.Column<string>(type: "text", nullable: false),
                    GuestPhone = table.Column<string>(type: "text", nullable: false),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    ReservedFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UserNotes = table.Column<string>(type: "text", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: true),
                    DepositPaid = table.Column<bool>(type: "boolean", nullable: false),
                    DepositAmountUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    DepositCurrencyCode = table.Column<string>(type: "text", nullable: false),
                    DepositForfeited = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservations_CustomerProfiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Reservations_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderRecordId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PreparedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    PreparedByRole = table.Column<string>(type: "text", nullable: false),
                    PreparedByName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderRecordId",
                        column: x => x.OrderRecordId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDayValidations_WorkDate",
                table: "AttendanceDayValidations",
                column: "WorkDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_PrimaryPhone",
                table: "CustomerProfiles",
                column: "PrimaryPhone");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_UniqueId",
                table: "CustomerProfiles",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendances_EmployeeId_WorkDate",
                table: "EmployeeAttendances",
                columns: new[] { "EmployeeId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_SignInId",
                table: "Employees",
                column: "SignInId",
                unique: true,
                filter: "\"SignInId\" IS NOT NULL AND \"SignInId\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UniqueId",
                table: "Employees",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_UniqueId",
                table: "InventoryItems",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderRecordId",
                table: "OrderItems",
                column: "OrderRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ServerId",
                table: "Orders",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TableId",
                table: "Orders",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UniqueId",
                table: "Orders",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPaymentRecords_EmployeeId_Year_Month",
                table: "PayrollPaymentRecords",
                columns: new[] { "EmployeeId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductIngredients_InventoryItemId",
                table: "ProductIngredients",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductIngredients_ProductId",
                table: "ProductIngredients",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_UniqueId",
                table: "Products",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CustomerProfileId",
                table: "Reservations",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ReservedFor",
                table: "Reservations",
                column: "ReservedFor");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Status",
                table: "Reservations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_TableId",
                table: "Reservations",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_UniqueId",
                table: "Reservations",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdvances_EmployeeId",
                table: "SalaryAdvances",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedOrderDrafts_EmployeeId_Portal_UpdatedAtUtc",
                table: "SharedOrderDrafts",
                columns: new[] { "EmployeeId", "Portal", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedOrderDrafts_UniqueId",
                table: "SharedOrderDrafts",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_AssignedServerId",
                table: "Tables",
                column: "AssignedServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_TableNumber",
                table: "Tables",
                column: "TableNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_UniqueId",
                table: "Tables",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TabletSessions_EmployeeId",
                table: "TabletSessions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TabletSessions_ExpiresAtUtc",
                table: "TabletSessions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date_Type",
                table: "Transactions",
                columns: new[] { "Date", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_CreatedAt",
                table: "WaitlistEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_Status",
                table: "WaitlistEntries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_UniqueId",
                table: "WaitlistEntries",
                column: "UniqueId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceDayValidations");

            migrationBuilder.DropTable(
                name: "EmployeeAttendances");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "PayrollPaymentRecords");

            migrationBuilder.DropTable(
                name: "ProductIngredients");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropTable(
                name: "SalaryAdvances");

            migrationBuilder.DropTable(
                name: "SharedOrderDrafts");

            migrationBuilder.DropTable(
                name: "TabletSessions");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "WaitlistEntries");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "CustomerProfiles");

            migrationBuilder.DropTable(
                name: "Tables");

            migrationBuilder.DropTable(
                name: "Employees");
        }
    }
}
