using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DirectoryManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AffiliateAccounts",
                columns: table => new
                {
                    AffiliateAccountId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferralCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    WalletAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PayoutCurrency = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateAccounts", x => x.AffiliateAccountId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlockedIPs",
                columns: table => new
                {
                    BlockedIPId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IpAddress = table.Column<string>(type: "character varying(75)", maxLength: 75, nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedIPs", x => x.BlockedIPId);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MetaDescription = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "ContentSnippets",
                columns: table => new
                {
                    ContentSnippetId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SnippetType = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentSnippets", x => x.ContentSnippetId);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryFilterLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    SubCategoryId = table.Column<int>(type: "integer", nullable: true),
                    TagIds = table.Column<string>(type: "text", nullable: true),
                    SearchTerm = table.Column<string>(type: "text", nullable: true),
                    CountryCode = table.Column<string>(type: "text", nullable: true),
                    Statuses = table.Column<string>(type: "text", nullable: true),
                    HasVideo = table.Column<bool>(type: "boolean", nullable: false),
                    HasTor = table.Column<bool>(type: "boolean", nullable: false),
                    HasI2p = table.Column<bool>(type: "boolean", nullable: false),
                    Page = table.Column<int>(type: "integer", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryFilterLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailCampaigns",
                columns: table => new
                {
                    EmailCampaignId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EmailCampaignKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SendMessagesPriorToSubscription = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaigns", x => x.EmailCampaignId);
                });

            migrationBuilder.CreateTable(
                name: "EmailMessages",
                columns: table => new
                {
                    EmailMessageId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmailKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EmailSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EmailBodyText = table.Column<string>(type: "text", nullable: false),
                    EmailBodyHtml = table.Column<string>(type: "text", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessages", x => x.EmailMessageId);
                });

            migrationBuilder.CreateTable(
                name: "EmailSendLogs",
                columns: table => new
                {
                    EmailSendLogId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceApplication = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    SentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSendLogs", x => x.EmailSendLogId);
                });

            migrationBuilder.CreateTable(
                name: "EmailSubscriptions",
                columns: table => new
                {
                    EmailSubscriptionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsSubscribed = table.Column<bool>(type: "boolean", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSubscriptions", x => x.EmailSubscriptionId);
                });

            migrationBuilder.CreateTable(
                name: "ExcludeUserAgents",
                columns: table => new
                {
                    ExcludeUserAgentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserAgent = table.Column<string>(type: "text", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExcludeUserAgents", x => x.ExcludeUserAgentId);
                });

            migrationBuilder.CreateTable(
                name: "LogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Message = table.Column<string>(type: "text", nullable: true),
                    MessageTemplate = table.Column<string>(type: "text", nullable: true),
                    Level = table.Column<string>(type: "text", nullable: true),
                    TimeStamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Exception = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessorConfigs",
                columns: table => new
                {
                    ProcessorConfigId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentProcessor = table.Column<int>(type: "integer", nullable: false),
                    UseProcessor = table.Column<bool>(type: "boolean", nullable: false),
                    Configuration = table.Column<string>(type: "text", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessorConfigs", x => x.ProcessorConfigId);
                });

            migrationBuilder.CreateTable(
                name: "Processors",
                columns: table => new
                {
                    ProcessorId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processors", x => x.ProcessorId);
                });

            migrationBuilder.CreateTable(
                name: "Raffles",
                columns: table => new
                {
                    RaffleId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Raffles", x => x.RaffleId);
                });

            migrationBuilder.CreateTable(
                name: "ReviewerKeys",
                columns: table => new
                {
                    ReviewerKeyId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublicKeyBlock = table.Column<string>(type: "text", nullable: false),
                    Alias = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewerKeys", x => x.ReviewerKeyId);
                });

            migrationBuilder.CreateTable(
                name: "ReviewTags",
                columns: table => new
                {
                    ReviewTagId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Level = table.Column<byte>(type: "smallint", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MinUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MaxUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewTags", x => x.ReviewTagId);
                });

            migrationBuilder.CreateTable(
                name: "SearchBlacklistTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Term = table.Column<string>(type: "text", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchBlacklistTerms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Term = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SponsoredListingOpeningNotifications",
                columns: table => new
                {
                    SponsoredListingOpeningNotificationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SponsorshipType = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<int>(type: "integer", nullable: true),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: true),
                    IsReminderSent = table.Column<bool>(type: "boolean", nullable: false),
                    SubscribedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ReminderSentDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReminderSentLink = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsoredListingOpeningNotifications", x => x.SponsoredListingOpeningNotificationId);
                });

            migrationBuilder.CreateTable(
                name: "SponsoredListingReservations",
                columns: table => new
                {
                    SponsoredListingReservationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReservationGuid = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpirationDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReservationGroup = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsoredListingReservations", x => x.SponsoredListingReservationId);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    TagId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.TagId);
                });

            migrationBuilder.CreateTable(
                name: "TrafficLogs",
                columns: table => new
                {
                    TrafficLogId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IpAddress = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    UserAgent = table.Column<string>(type: "text", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrafficLogs", x => x.TrafficLogId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(36)", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "character varying(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(36)", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    Discriminator = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(36)", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subcategories",
                columns: table => new
                {
                    SubCategoryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SubCategoryKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MetaDescription = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    PageDetails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RequireReviewVerification = table.Column<bool>(type: "boolean", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subcategories", x => x.SubCategoryId);
                    table.ForeignKey(
                        name: "FK_Subcategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailCampaignMessages",
                columns: table => new
                {
                    EmailCampaignMessageId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmailCampaignId = table.Column<int>(type: "integer", nullable: false),
                    EmailMessageId = table.Column<int>(type: "integer", nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaignMessages", x => x.EmailCampaignMessageId);
                    table.ForeignKey(
                        name: "FK_EmailCampaignMessages_EmailCampaigns_EmailCampaignId",
                        column: x => x.EmailCampaignId,
                        principalTable: "EmailCampaigns",
                        principalColumn: "EmailCampaignId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailCampaignMessages_EmailMessages_EmailMessageId",
                        column: x => x.EmailMessageId,
                        principalTable: "EmailMessages",
                        principalColumn: "EmailMessageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailCampaignSubscriptions",
                columns: table => new
                {
                    EmailCampaignSubscriptionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmailCampaignId = table.Column<int>(type: "integer", nullable: false),
                    EmailSubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    SubscribedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaignSubscriptions", x => x.EmailCampaignSubscriptionId);
                    table.ForeignKey(
                        name: "FK_EmailCampaignSubscriptions_EmailCampaigns_EmailCampaignId",
                        column: x => x.EmailCampaignId,
                        principalTable: "EmailCampaigns",
                        principalColumn: "EmailCampaignId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailCampaignSubscriptions_EmailSubscriptions_EmailSubscrip~",
                        column: x => x.EmailSubscriptionId,
                        principalTable: "EmailSubscriptions",
                        principalColumn: "EmailSubscriptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SentEmailRecords",
                columns: table => new
                {
                    SentEmailRecordId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmailSubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    EmailMessageId = table.Column<int>(type: "integer", nullable: false),
                    SentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDelivered = table.Column<bool>(type: "boolean", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentEmailRecords", x => x.SentEmailRecordId);
                    table.ForeignKey(
                        name: "FK_SentEmailRecords_EmailMessages_EmailMessageId",
                        column: x => x.EmailMessageId,
                        principalTable: "EmailMessages",
                        principalColumn: "EmailMessageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SentEmailRecords_EmailSubscriptions_EmailSubscriptionId",
                        column: x => x.EmailSubscriptionId,
                        principalTable: "EmailSubscriptions",
                        principalColumn: "EmailSubscriptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryEntries",
                columns: table => new
                {
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DirectoryEntryKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LinkA = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Link2 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Link2A = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Link3 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Link3A = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProofLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VideoLink = table.Column<string>(type: "text", nullable: true),
                    DirectoryStatus = table.Column<int>(type: "integer", nullable: false),
                    DirectoryBadge = table.Column<int>(type: "integer", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Processor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Messenger = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Social = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PgpKey = table.Column<string>(type: "text", nullable: true),
                    SubCategoryId = table.Column<int>(type: "integer", nullable: false),
                    FoundedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReviewsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntries", x => x.DirectoryEntryId);
                    table.ForeignKey(
                        name: "FK_DirectoryEntries_Subcategories_SubCategoryId",
                        column: x => x.SubCategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "SubCategoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryEntriesAudit",
                columns: table => new
                {
                    DirectoryEntriesAuditId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Link2 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Link3 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProofLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VideoLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    FoundedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DirectoryStatus = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Processor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Messenger = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Social = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SubCategoryId = table.Column<int>(type: "integer", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntriesAudit", x => x.DirectoryEntriesAuditId);
                    table.ForeignKey(
                        name: "FK_DirectoryEntriesAudit_Subcategories_SubCategoryId",
                        column: x => x.SubCategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "SubCategoryId");
                });

            migrationBuilder.CreateTable(
                name: "SponsoredListingOffers",
                columns: table => new
                {
                    SponsoredListingOfferId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    PriceCurrency = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(20,12)", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    SubcategoryId = table.Column<int>(type: "integer", nullable: true),
                    SponsorshipType = table.Column<int>(type: "integer", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsoredListingOffers", x => x.SponsoredListingOfferId);
                    table.ForeignKey(
                        name: "FK_SponsoredListingOffers_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "CategoryId");
                    table.ForeignKey(
                        name: "FK_SponsoredListingOffers_Subcategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "SubCategoryId");
                });

            migrationBuilder.CreateTable(
                name: "AdditionalLinks",
                columns: table => new
                {
                    AdditionalLinkId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdditionalLinks", x => x.AdditionalLinkId);
                    table.ForeignKey(
                        name: "FK_AdditionalLinks_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateCommissionsEarned",
                columns: table => new
                {
                    AffiliateCommissionEarnedId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    CommissionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsdValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentCurrency = table.Column<int>(type: "integer", nullable: false),
                    PaymentCurrencyAmount = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    TransactionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateCommissionsEarned", x => x.AffiliateCommissionEarnedId);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissionsEarned_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryEntryReviews",
                columns: table => new
                {
                    DirectoryEntryReviewId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    AuthorFingerprint = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AuthorPublicKeyArmor = table.Column<string>(type: "text", nullable: true),
                    AuthorHandle = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DisplayNameSignatureArmor = table.Column<string>(type: "text", nullable: true),
                    Rating = table.Column<byte>(type: "smallint", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    RejectionReason = table.Column<string>(type: "text", nullable: false),
                    ModerationStatus = table.Column<int>(type: "integer", nullable: false),
                    PostSignatureHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DeletionSignatureHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceIpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OrderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OrderUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    OrderProofContext = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    AutoModerationResult = table.Column<int>(type: "integer", nullable: false),
                    AutoModerationReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AutoModeratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AutoModerationAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAutoModerationAttemptUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedOrderUsdValue = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    IsOfficial = table.Column<bool>(type: "boolean", nullable: false),
                    TestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SendingTxUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ReceivingTxUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    AmlScreenshotUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntryReviews", x => x.DirectoryEntryReviewId);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviews_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryEntrySelections",
                columns: table => new
                {
                    DirectoryEntrySelectionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    EntrySelectionType = table.Column<int>(type: "integer", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntrySelections", x => x.DirectoryEntrySelectionId);
                    table.ForeignKey(
                        name: "FK_DirectoryEntrySelections_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryEntryTags",
                columns: table => new
                {
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    TagId = table.Column<int>(type: "integer", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntryTags", x => new { x.DirectoryEntryId, x.TagId });
                    table.ForeignKey(
                        name: "FK_DirectoryEntryTags_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "TagId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SponsoredListingInvoices",
                columns: table => new
                {
                    SponsoredListingInvoiceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceDescription = table.Column<string>(type: "text", nullable: false),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    CampaignStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CampaignEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(20,12)", nullable: false),
                    OutcomeAmount = table.Column<decimal>(type: "numeric(20,12)", nullable: false),
                    PaidInCurrency = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(20,12)", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    PaymentProcessor = table.Column<int>(type: "integer", nullable: false),
                    ProcessorInvoiceId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PaymentStatus = table.Column<int>(type: "integer", nullable: false),
                    InvoiceRequest = table.Column<string>(type: "text", nullable: false),
                    InvoiceResponse = table.Column<string>(type: "text", nullable: false),
                    PaymentResponse = table.Column<string>(type: "text", nullable: false),
                    SponsoredListingId = table.Column<int>(type: "integer", nullable: true),
                    SponsorshipType = table.Column<int>(type: "integer", nullable: false),
                    SubCategoryId = table.Column<int>(type: "integer", nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ReservationGuid = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferralCodeUsed = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    IsReminderSent = table.Column<bool>(type: "boolean", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsoredListingInvoices", x => x.SponsoredListingInvoiceId);
                    table.ForeignKey(
                        name: "FK_SponsoredListingInvoices_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                columns: table => new
                {
                    SubmissionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubmissionStatus = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Link2 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Link3 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProofLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VideoLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Processor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PgpKey = table.Column<string>(type: "text", nullable: true),
                    NoteToAdmin = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Messenger = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Social = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SubCategoryId = table.Column<int>(type: "integer", nullable: true),
                    SuggestedSubCategory = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: true),
                    DirectoryStatus = table.Column<int>(type: "integer", nullable: true),
                    Tags = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    SelectedTagIdsCsv = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RelatedLinksJson = table.Column<string>(type: "text", nullable: true),
                    FoundedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.SubmissionId);
                    table.ForeignKey(
                        name: "FK_Submissions_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId");
                    table.ForeignKey(
                        name: "FK_Submissions_Subcategories_SubCategoryId",
                        column: x => x.SubCategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "SubCategoryId");
                });

            migrationBuilder.CreateTable(
                name: "VerificationRequests",
                columns: table => new
                {
                    VerificationRequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceIpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationRequests", x => x.VerificationRequestId);
                    table.ForeignKey(
                        name: "FK_VerificationRequests_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryEntryReviewComments",
                columns: table => new
                {
                    DirectoryEntryReviewCommentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryReviewId = table.Column<int>(type: "integer", nullable: false),
                    ParentCommentId = table.Column<int>(type: "integer", nullable: true),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ModerationStatus = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    AuthorFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntryReviewComments", x => x.DirectoryEntryReviewCommentId);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviewComments_DirectoryEntryReviewComments_P~",
                        column: x => x.ParentCommentId,
                        principalTable: "DirectoryEntryReviewComments",
                        principalColumn: "DirectoryEntryReviewCommentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviewComments_DirectoryEntryReviews_Director~",
                        column: x => x.DirectoryEntryReviewId,
                        principalTable: "DirectoryEntryReviews",
                        principalColumn: "DirectoryEntryReviewId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryEntryReviewRaffleEntries",
                columns: table => new
                {
                    DirectoryEntryReviewRaffleEntryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectoryEntryReviewId = table.Column<int>(type: "integer", nullable: false),
                    RaffleId = table.Column<int>(type: "integer", nullable: true),
                    CryptoType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CryptoAddress = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntryReviewRaffleEntries", x => x.DirectoryEntryReviewRaffleEntryId);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviewRaffleEntries_DirectoryEntryReviews_Dir~",
                        column: x => x.DirectoryEntryReviewId,
                        principalTable: "DirectoryEntryReviews",
                        principalColumn: "DirectoryEntryReviewId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviewRaffleEntries_Raffles_RaffleId",
                        column: x => x.RaffleId,
                        principalTable: "Raffles",
                        principalColumn: "RaffleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryEntryReviewTags",
                columns: table => new
                {
                    DirectoryEntryReviewId = table.Column<int>(type: "integer", nullable: false),
                    ReviewTagId = table.Column<int>(type: "integer", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryEntryReviewTags", x => new { x.DirectoryEntryReviewId, x.ReviewTagId });
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviewTags_DirectoryEntryReviews_DirectoryEnt~",
                        column: x => x.DirectoryEntryReviewId,
                        principalTable: "DirectoryEntryReviews",
                        principalColumn: "DirectoryEntryReviewId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectoryEntryReviewTags_ReviewTags_ReviewTagId",
                        column: x => x.ReviewTagId,
                        principalTable: "ReviewTags",
                        principalColumn: "ReviewTagId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateCommissions",
                columns: table => new
                {
                    AffiliateCommissionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SponsoredListingInvoiceId = table.Column<int>(type: "integer", nullable: false),
                    AffiliateAccountId = table.Column<int>(type: "integer", nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    PayoutCurrency = table.Column<int>(type: "integer", nullable: false),
                    PayoutStatus = table.Column<int>(type: "integer", nullable: false),
                    PayoutTransactionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateCommissions", x => x.AffiliateCommissionId);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissions_AffiliateAccounts_AffiliateAccountId",
                        column: x => x.AffiliateAccountId,
                        principalTable: "AffiliateAccounts",
                        principalColumn: "AffiliateAccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissions_SponsoredListingInvoices_SponsoredList~",
                        column: x => x.SponsoredListingInvoiceId,
                        principalTable: "SponsoredListingInvoices",
                        principalColumn: "SponsoredListingInvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SponsoredListings",
                columns: table => new
                {
                    SponsoredListingId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CampaignEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DirectoryEntryId = table.Column<int>(type: "integer", nullable: false),
                    SponsoredListingInvoiceId = table.Column<int>(type: "integer", nullable: false),
                    SponsorshipType = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    SubCategoryId = table.Column<int>(type: "integer", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsoredListings", x => x.SponsoredListingId);
                    table.ForeignKey(
                        name: "FK_SponsoredListings_DirectoryEntries_DirectoryEntryId",
                        column: x => x.DirectoryEntryId,
                        principalTable: "DirectoryEntries",
                        principalColumn: "DirectoryEntryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SponsoredListings_SponsoredListingInvoices_SponsoredListing~",
                        column: x => x.SponsoredListingInvoiceId,
                        principalTable: "SponsoredListingInvoices",
                        principalColumn: "SponsoredListingInvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdditionalLinks_DirectoryEntryId_SortOrder",
                table: "AdditionalLinks",
                columns: new[] { "DirectoryEntryId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AffiliateAccount_ReferralCode",
                table: "AffiliateAccounts",
                column: "ReferralCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissions_AffiliateAccountId",
                table: "AffiliateCommissions",
                column: "AffiliateAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_AffiliateCommission_Invoice",
                table: "AffiliateCommissions",
                column: "SponsoredListingInvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionEarned_Date",
                table: "AffiliateCommissionsEarned",
                column: "CommissionDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionEarned_Entry_Date",
                table: "AffiliateCommissionsEarned",
                columns: new[] { "DirectoryEntryId", "CommissionDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionEarned_TransactionId",
                table: "AffiliateCommissionsEarned",
                column: "TransactionId",
                filter: "\"TransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlockedIPs_IpAddress",
                table: "BlockedIPs",
                column: "IpAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CategoryKey",
                table: "Categories",
                column: "CategoryKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_DirectoryEntryKey",
                table: "DirectoryEntries",
                column: "DirectoryEntryKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_Link",
                table: "DirectoryEntries",
                column: "Link",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_Status",
                table: "DirectoryEntries",
                column: "DirectoryStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_SubCategoryId",
                table: "DirectoryEntries",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntries_Update_Create",
                table: "DirectoryEntries",
                columns: new[] { "UpdateDate", "CreateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntriesAudit_SubCategoryId",
                table: "DirectoryEntriesAudit",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewComments_Mod_Review",
                table: "DirectoryEntryReviewComments",
                columns: new[] { "ModerationStatus", "DirectoryEntryReviewId" })
                .Annotation("Npgsql:IndexInclude", new[] { "CreateDate", "UpdateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewComments_ParentCommentId",
                table: "DirectoryEntryReviewComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewComments_Review_Mod_Create_Id",
                table: "DirectoryEntryReviewComments",
                columns: new[] { "DirectoryEntryReviewId", "ModerationStatus", "CreateDate", "DirectoryEntryReviewCommentId" });

            migrationBuilder.CreateIndex(
                name: "IX_RaffleEntry_RaffleId",
                table: "DirectoryEntryReviewRaffleEntries",
                column: "RaffleId");

            migrationBuilder.CreateIndex(
                name: "IX_RaffleEntry_Status_Create_Id",
                table: "DirectoryEntryReviewRaffleEntries",
                columns: new[] { "Status", "CreateDate", "DirectoryEntryReviewRaffleEntryId" });

            migrationBuilder.CreateIndex(
                name: "UX_RaffleEntry_Review",
                table: "DirectoryEntryReviewRaffleEntries",
                column: "DirectoryEntryReviewId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviews_AuthorFingerprint",
                table: "DirectoryEntryReviews",
                column: "AuthorFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Entry_Mod_Create_Id",
                table: "DirectoryEntryReviews",
                columns: new[] { "DirectoryEntryId", "ModerationStatus", "CreateDate", "DirectoryEntryReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Entry_Official_Mod",
                table: "DirectoryEntryReviews",
                columns: new[] { "DirectoryEntryId", "IsOfficial", "ModerationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Mod_Entry",
                table: "DirectoryEntryReviews",
                columns: new[] { "ModerationStatus", "DirectoryEntryId" })
                .Annotation("Npgsql:IndexInclude", new[] { "CreateDate", "UpdateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryReviewTags_ReviewTagId",
                table: "DirectoryEntryReviewTags",
                column: "ReviewTagId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntrySelections_DirectoryEntryId",
                table: "DirectoryEntrySelections",
                column: "DirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryEntryTags_Tag_Entry",
                table: "DirectoryEntryTags",
                columns: new[] { "TagId", "DirectoryEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignMessages_EmailCampaignId",
                table: "EmailCampaignMessages",
                column: "EmailCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignMessages_EmailMessageId",
                table: "EmailCampaignMessages",
                column: "EmailMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaigns_EmailCampaignKey",
                table: "EmailCampaigns",
                column: "EmailCampaignKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignSubscriptions_EmailCampaignId_IsActive",
                table: "EmailCampaignSubscriptions",
                columns: new[] { "EmailCampaignId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignSubscriptions_EmailSubscriptionId",
                table: "EmailCampaignSubscriptions",
                column: "EmailSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_EmailKey",
                table: "EmailMessages",
                column: "EmailKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailSendLogs_SentDate",
                table: "EmailSendLogs",
                column: "SentDate");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSendLogs_SourceApplication_SentDate",
                table: "EmailSendLogs",
                columns: new[] { "SourceApplication", "SentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailSubscriptions_Email",
                table: "EmailSubscriptions",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailSubscriptions_IsSubscribed",
                table: "EmailSubscriptions",
                column: "IsSubscribed");

            migrationBuilder.CreateIndex(
                name: "IX_ExcludeUserAgents_UserAgent",
                table: "ExcludeUserAgents",
                column: "UserAgent",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessorConfigs_PaymentProcessor",
                table: "ProcessorConfigs",
                column: "PaymentProcessor",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processors_Name",
                table: "Processors",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Raffle_IsEnabled",
                table: "Raffles",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_Raffle_StartEnd",
                table: "Raffles",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "UX_Raffle_Name",
                table: "Raffles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewerKeys_Fingerprint",
                table: "ReviewerKeys",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewTags_Slug",
                table: "ReviewTags",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchBlacklistTerms_Term",
                table: "SearchBlacklistTerms",
                column: "Term",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SentEmailRecords_EmailMessageId",
                table: "SentEmailRecords",
                column: "EmailMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_SentEmailRecords_EmailSubscriptionId_EmailMessageId",
                table: "SentEmailRecords",
                columns: new[] { "EmailSubscriptionId", "EmailMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_Dir_PaidStatus",
                table: "SponsoredListingInvoices",
                columns: new[] { "DirectoryEntryId", "PaymentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingInvoices_InvoiceId",
                table: "SponsoredListingInvoices",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_CategoryId",
                table: "SponsoredListingOffers",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_SponsorshipType_Days",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days" },
                unique: true,
                filter: "\"SubcategoryId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOffers_SubcategoryId",
                table: "SponsoredListingOffers",
                column: "SubcategoryId");

            migrationBuilder.CreateIndex(
                name: "UX_Offer_Type_Days_Cat_NoSubcat",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days", "CategoryId" },
                unique: true,
                filter: "\"SubcategoryId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Offer_Type_Days_Cat_Subcat",
                table: "SponsoredListingOffers",
                columns: new[] { "SponsorshipType", "Days", "CategoryId", "SubcategoryId" },
                unique: true,
                filter: "\"SubcategoryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOpeningNotification_Queue",
                table: "SponsoredListingOpeningNotifications",
                columns: new[] { "SubscribedDate", "SponsoredListingOpeningNotificationId" },
                filter: "\"IsActive\" = TRUE AND \"IsReminderSent\" = FALSE")
                .Annotation("Npgsql:IndexInclude", new[] { "Email", "SponsorshipType", "TypeId", "DirectoryEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOpeningNotification_Unique",
                table: "SponsoredListingOpeningNotifications",
                columns: new[] { "Email", "SponsorshipType", "TypeId", "SubscribedDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOpeningNotifications_Email_SponsorshipType_~",
                table: "SponsoredListingOpeningNotifications",
                columns: new[] { "Email", "SponsorshipType", "TypeId", "DirectoryEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingOpeningNotifications_SponsorshipType_TypeId~",
                table: "SponsoredListingOpeningNotifications",
                columns: new[] { "SponsorshipType", "TypeId", "IsActive", "IsReminderSent", "SubscribedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingReservations_ExpirationDateTime",
                table: "SponsoredListingReservations",
                column: "ExpirationDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListingReservations_ReservationGuid",
                table: "SponsoredListingReservations",
                column: "ReservationGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListings_CampaignEndDate",
                table: "SponsoredListings",
                column: "CampaignEndDate");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListings_CreateDate_UpdateDate",
                table: "SponsoredListings",
                columns: new[] { "CreateDate", "UpdateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListings_DirectoryEntryId",
                table: "SponsoredListings",
                column: "DirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListings_End_Start",
                table: "SponsoredListings",
                columns: new[] { "CampaignEndDate", "CampaignStartDate" },
                descending: new bool[0])
                .Annotation("Npgsql:IndexInclude", new[] { "DirectoryEntryId", "SponsorshipType" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListings_SponsoredListingInvoiceId",
                table: "SponsoredListings",
                column: "SponsoredListingInvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredListings_Type_End_Start",
                table: "SponsoredListings",
                columns: new[] { "SponsorshipType", "CampaignEndDate", "CampaignStartDate" },
                descending: new[] { false, true, true })
                .Annotation("Npgsql:IndexInclude", new[] { "DirectoryEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Subcategories_CategoryId",
                table: "Subcategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Subcategories_SubCategoryKey_CategoryId",
                table: "Subcategories",
                columns: new[] { "SubCategoryKey", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_DirectoryEntryId",
                table: "Submissions",
                column: "DirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_SubCategoryId",
                table: "Submissions",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Key",
                table: "Tags",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrafficLog_CreateDate",
                table: "TrafficLogs",
                column: "CreateDate");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRequests_DirectoryEntryId",
                table: "VerificationRequests",
                column: "DirectoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRequests_Status_Create_Id",
                table: "VerificationRequests",
                columns: new[] { "Status", "CreateDate", "VerificationRequestId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdditionalLinks");

            migrationBuilder.DropTable(
                name: "AffiliateCommissions");

            migrationBuilder.DropTable(
                name: "AffiliateCommissionsEarned");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BlockedIPs");

            migrationBuilder.DropTable(
                name: "ContentSnippets");

            migrationBuilder.DropTable(
                name: "DirectoryEntriesAudit");

            migrationBuilder.DropTable(
                name: "DirectoryEntryReviewComments");

            migrationBuilder.DropTable(
                name: "DirectoryEntryReviewRaffleEntries");

            migrationBuilder.DropTable(
                name: "DirectoryEntryReviewTags");

            migrationBuilder.DropTable(
                name: "DirectoryEntrySelections");

            migrationBuilder.DropTable(
                name: "DirectoryEntryTags");

            migrationBuilder.DropTable(
                name: "DirectoryFilterLogs");

            migrationBuilder.DropTable(
                name: "EmailCampaignMessages");

            migrationBuilder.DropTable(
                name: "EmailCampaignSubscriptions");

            migrationBuilder.DropTable(
                name: "EmailSendLogs");

            migrationBuilder.DropTable(
                name: "ExcludeUserAgents");

            migrationBuilder.DropTable(
                name: "LogEntries");

            migrationBuilder.DropTable(
                name: "ProcessorConfigs");

            migrationBuilder.DropTable(
                name: "Processors");

            migrationBuilder.DropTable(
                name: "ReviewerKeys");

            migrationBuilder.DropTable(
                name: "SearchBlacklistTerms");

            migrationBuilder.DropTable(
                name: "SearchLogs");

            migrationBuilder.DropTable(
                name: "SentEmailRecords");

            migrationBuilder.DropTable(
                name: "SponsoredListingOffers");

            migrationBuilder.DropTable(
                name: "SponsoredListingOpeningNotifications");

            migrationBuilder.DropTable(
                name: "SponsoredListingReservations");

            migrationBuilder.DropTable(
                name: "SponsoredListings");

            migrationBuilder.DropTable(
                name: "Submissions");

            migrationBuilder.DropTable(
                name: "TrafficLogs");

            migrationBuilder.DropTable(
                name: "VerificationRequests");

            migrationBuilder.DropTable(
                name: "AffiliateAccounts");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Raffles");

            migrationBuilder.DropTable(
                name: "DirectoryEntryReviews");

            migrationBuilder.DropTable(
                name: "ReviewTags");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "EmailCampaigns");

            migrationBuilder.DropTable(
                name: "EmailMessages");

            migrationBuilder.DropTable(
                name: "EmailSubscriptions");

            migrationBuilder.DropTable(
                name: "SponsoredListingInvoices");

            migrationBuilder.DropTable(
                name: "DirectoryEntries");

            migrationBuilder.DropTable(
                name: "Subcategories");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
