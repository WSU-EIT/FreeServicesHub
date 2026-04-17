using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeServicesHub.EFModels.Migrations
{
    /// <inheritdoc />
    public partial class initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentHeartbeats",
                columns: table => new
                {
                    HeartbeatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime", nullable: false),
                    CpuPercent = table.Column<double>(type: "float", nullable: false),
                    MemoryPercent = table.Column<double>(type: "float", nullable: false),
                    MemoryUsedGB = table.Column<double>(type: "float", nullable: false),
                    MemoryTotalGB = table.Column<double>(type: "float", nullable: false),
                    DiskMetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentHeartbeats", x => x.HeartbeatId);
                });

            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Hostname = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OperatingSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Architecture = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AgentVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DotNetVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastHeartbeat = table.Column<DateTime>(type: "datetime", nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    RegisteredBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Added = table.Column<DateTime>(type: "datetime", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.AgentId);
                });

            migrationBuilder.CreateTable(
                name: "ApiClientTokens",
                columns: table => new
                {
                    ApiClientTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TokenPrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    RevokedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiClientTokens", x => x.ApiClientTokenId);
                });

            migrationBuilder.CreateTable(
                name: "CiCdTokenUsages",
                columns: table => new
                {
                    CiCdTokenUsageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TokenPrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MaxUses = table.Column<int>(type: "int", nullable: false),
                    UsesConsumed = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime", nullable: false),
                    InvalidatedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CiCdTokenUsages", x => x.CiCdTokenUsageId);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentGroups",
                columns: table => new
                {
                    DepartmentGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentGroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Added = table.Column<DateTime>(type: "datetime", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentGroups", x => x.DepartmentGroupId);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActiveDirectoryNames = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    DepartmentGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Added = table.Column<DateTime>(type: "datetime", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.DepartmentId);
                });

            migrationBuilder.CreateTable(
                name: "HubJobs",
                columns: table => new
                {
                    HubJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HubJobs", x => x.HubJobId);
                });

            migrationBuilder.CreateTable(
                name: "PluginCache",
                columns: table => new
                {
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Author = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Namespace = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClassName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdditionalAssemblies = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StillExists = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginCache", x => x.RecordId);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationKeys",
                columns: table => new
                {
                    RegistrationKeyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    KeyPrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    Used = table.Column<bool>(type: "bit", nullable: false),
                    UsedByAgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    Created = table.Column<DateTime>(type: "datetime", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationKeys", x => x.RegistrationKeyId);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    SettingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettingType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SettingNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SettingText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings_1", x => x.SettingId);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Style = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    UseInAppointments = table.Column<bool>(type: "bit", nullable: false),
                    UseInEmailTemplates = table.Column<bool>(type: "bit", nullable: false),
                    UseInServices = table.Column<bool>(type: "bit", nullable: false),
                    Added = table.Column<DateTime>(type: "datetime", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.TagId);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenantCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Added = table.Column<DateTime>(type: "datetime", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "UDFLabels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UDF = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShowColumn = table.Column<bool>(type: "bit", nullable: true),
                    ShowInFilter = table.Column<bool>(type: "bit", nullable: true),
                    IncludeInSearch = table.Column<bool>(type: "bit", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UDFLabels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserGroups",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Settings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Added = table.Column<DateTime>(type: "datetime", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroups", x => x.GroupId);
                });

            migrationBuilder.CreateTable(
                name: "TagItems",
                columns: table => new
                {
                    TagItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagItems", x => x.TagItemId);
                    table.ForeignKey(
                        name: "FK_TagItems_Tags",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "TagId");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmployeeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "datetime", nullable: true),
                    LastLoginSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Admin = table.Column<bool>(type: "bit", nullable: false),
                    ManageFiles = table.Column<bool>(type: "bit", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreventPasswordChange = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: true),
                    LastLockoutDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UDF01 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UDF02 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UDF03 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UDF04 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UDF05 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UDF06 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UDF07 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UDF08 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UDF09 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UDF10 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Added = table.Column<DateTime>(type: "datetime", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    Preferences = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.ForeignKey(
                        name: "IX_Users_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "DepartmentId");
                    table.ForeignKey(
                        name: "IX_Users_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId");
                });

            migrationBuilder.CreateTable(
                name: "FileStorage",
                columns: table => new
                {
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: true),
                    Value = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    UploadDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceFileId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileStorage", x => x.FileId);
                    table.ForeignKey(
                        name: "IX_FileStorage_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "UserInGroups",
                columns: table => new
                {
                    UserInGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInGroups", x => x.UserInGroupId);
                    table.ForeignKey(
                        name: "FK_UserInGroups_UserGroups",
                        column: x => x.GroupId,
                        principalTable: "UserGroups",
                        principalColumn: "GroupId");
                    table.ForeignKey(
                        name: "FK_UserInGroups_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileStorage_UserId",
                table: "FileStorage",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TagItems_TagId",
                table: "TagItems",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInGroups_GroupId",
                table: "UserInGroups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInGroups_UserId",
                table: "UserInGroups",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DepartmentId",
                table: "Users",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentHeartbeats");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "ApiClientTokens");

            migrationBuilder.DropTable(
                name: "CiCdTokenUsages");

            migrationBuilder.DropTable(
                name: "DepartmentGroups");

            migrationBuilder.DropTable(
                name: "FileStorage");

            migrationBuilder.DropTable(
                name: "HubJobs");

            migrationBuilder.DropTable(
                name: "PluginCache");

            migrationBuilder.DropTable(
                name: "RegistrationKeys");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "TagItems");

            migrationBuilder.DropTable(
                name: "UDFLabels");

            migrationBuilder.DropTable(
                name: "UserInGroups");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "UserGroups");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
