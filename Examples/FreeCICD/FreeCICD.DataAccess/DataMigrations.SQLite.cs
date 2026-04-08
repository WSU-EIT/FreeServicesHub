namespace FreeCICD;
public partial class DataMigrations
{
    public List<DataObjects.DataMigration> GetMigrationsSQLite()
    {
        List<DataObjects.DataMigration> output = new List<DataObjects.DataMigration>();

        List<string> m1 = new List<string>();
        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            )
            """);

        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "DepartmentGroups" (
                "DepartmentGroupId" TEXT NOT NULL CONSTRAINT "PK_DepartmentGroups" PRIMARY KEY,
                "DepartmentGroupName" TEXT NULL,
                "TenantId" TEXT NOT NULL,
                "Added" datetime NOT NULL,
                "AddedBy" TEXT NULL,
                "LastModified" datetime NOT NULL,
                "LastModifiedBy" TEXT NULL,
                "Deleted" INTEGER NOT NULL,
                "DeletedAt" datetime NULL
            )
            """);

        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "Departments" (
                "DepartmentId" TEXT NOT NULL CONSTRAINT "PK_Departments" PRIMARY KEY,
                "DepartmentName" TEXT NOT NULL,
                "ActiveDirectoryNames" TEXT NULL,
                "Enabled" INTEGER NOT NULL,
                "DepartmentGroupId" TEXT NULL,
                "TenantId" TEXT NOT NULL,
                "Added" datetime NOT NULL,
                "AddedBy" TEXT NULL,
                "LastModified" datetime NOT NULL,
                "LastModifiedBy" TEXT NULL,
                "Deleted" INTEGER NOT NULL,
                "DeletedAt" datetime NULL
            )
            """);



        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "PluginCache" (
                "RecordId" TEXT NOT NULL CONSTRAINT "PK_PluginCache" PRIMARY KEY,
                "Id" TEXT NOT NULL,
                "Author" TEXT NULL,
                "Name" TEXT NULL,
                "Type" TEXT NULL,
                "Version" TEXT NULL,
                "Properties" TEXT NULL,
                "Namespace" TEXT NULL,
                "ClassName" TEXT NULL,
                "Code" TEXT NULL,
                "AdditionalAssemblies" TEXT NULL,
                "StillExists" INTEGER NOT NULL
            );
            """);


        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "Settings" (
                "SettingId" INTEGER NOT NULL CONSTRAINT "PK_Settings_1" PRIMARY KEY AUTOINCREMENT,
                "SettingName" TEXT NOT NULL,
                "SettingType" TEXT NULL,
                "SettingNotes" TEXT NULL,
                "SettingText" TEXT NULL,
                "TenantId" TEXT NULL,
                "UserId" TEXT NULL,
                "LastModified" datetime NOT NULL,
                "LastModifiedBy" TEXT NULL
            )
            """);

        // {{ModuleItemStart:Tags}}
        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "Tags" (
                "TagId" TEXT NOT NULL CONSTRAINT "PK_Tags" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Style" TEXT NULL,
                "Enabled" INTEGER NOT NULL,
                "Added" datetime NOT NULL,
                "AddedBy" TEXT NULL,
                "LastModified" datetime NOT NULL,
                "LastModifiedBy" TEXT NULL,
                "Deleted" INTEGER NOT NULL,
                "DeletedAt" datetime NULL
            );
            """);
        // {{ModuleItemEnd:Tags}}

        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "Tenants" (
                "TenantId" TEXT NOT NULL CONSTRAINT "PK_Tenants" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "TenantCode" TEXT NOT NULL,
                "Enabled" INTEGER NOT NULL,
                "Added" datetime NOT NULL,
                "AddedBy" TEXT NULL,
                "LastModified" datetime NOT NULL,
                "LastModifiedBy" TEXT NULL
            )
            """);

        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "UDFLabels" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_UDFLabels" PRIMARY KEY,
                "Module" TEXT NOT NULL,
                "UDF" TEXT NOT NULL,
                "Label" TEXT NULL,
                "ShowColumn" INTEGER NULL,
                "ShowInFilter" INTEGER NULL,
                "IncludeInSearch" INTEGER NULL,
                "TenantId" TEXT NOT NULL,
                "LastModified" datetime NOT NULL,
                "LastModifiedBy" TEXT NULL
            )
            """);

        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "UserGroups" (
                "GroupId" TEXT NOT NULL CONSTRAINT "PK_UserGroups" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Enabled" INTEGER NOT NULL,
                "Settings" TEXT NULL,
                "Added" datetime NOT NULL,
                "AddedBy" TEXT NULL,
                "LastModified" datetime NOT NULL,
                "LastModifiedBy" TEXT NULL,
                "Deleted" INTEGER NOT NULL,
                "DeletedAt" datetime NULL
            );
            """);


        // {{ModuleItemStart:Tags}}
        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "TagItems" (
                "TagItemId" TEXT NOT NULL CONSTRAINT "PK_TagItems" PRIMARY KEY,
                "TagId" TEXT NOT NULL,
                "TenantId" TEXT NOT NULL,
                "ItemId" TEXT NOT NULL,
                CONSTRAINT "FK_TagItems_Tags" FOREIGN KEY ("TagId") REFERENCES "Tags" ("TagId")
            );
            """);
        // {{ModuleItemEnd:Tags}}

        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "Users" (
                "UserId" TEXT NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "FirstName" TEXT NULL,
                "LastName" TEXT NULL,
                "Email" TEXT NOT NULL,
                "Phone" TEXT NULL,
                "Username" TEXT NOT NULL,
                "EmployeeId" TEXT NULL,
                "DepartmentId" TEXT NULL,
                "Title" TEXT NULL,
                "Location" TEXT NULL,
                "Enabled" INTEGER NOT NULL,
                "LastLogin" datetime NULL,
                "LastLoginSource" TEXT NULL,
                "Admin" INTEGER NOT NULL,
                "CanBeScheduled" INTEGER NOT NULL,
                "ManageFiles" INTEGER NOT NULL,
                "ManageAppointments" INTEGER NOT NULL,
                "Password" TEXT NULL,
                "PreventPasswordChange" INTEGER NOT NULL,
                "FailedLoginAttempts" INTEGER NULL,
                "LastLockoutDate" datetime NULL,
                "Source" TEXT NULL,
                "UDF01" TEXT NULL,
                "UDF02" TEXT NULL,
                "UDF03" TEXT NULL,
                "UDF04" TEXT NULL,
                "UDF05" TEXT NULL,
                "UDF06" TEXT NULL,
                "UDF07" TEXT NULL,
                "UDF08" TEXT NULL,
                "UDF09" TEXT NULL,
                "UDF10" TEXT NULL,
                "Added" datetime NOT NULL,
                "AddedBy" TEXT NULL,
                "LastModified" datetime NOT NULL,
                "LastModifiedBy" TEXT NULL,
                "Deleted" INTEGER NOT NULL,
                "DeletedAt" datetime NULL,
                "Preferences" TEXT NULL,
                CONSTRAINT "IX_Users_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Departments" ("DepartmentId"),
                CONSTRAINT "IX_Users_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("TenantId")
            )
            """);


        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "FileStorage" (
                "FileId" TEXT NOT NULL CONSTRAINT "PK_FileStorage" PRIMARY KEY,
                "ItemId" TEXT NULL,
                "FileName" TEXT NOT NULL,
                "Extension" TEXT NOT NULL,
                "Bytes" INTEGER NULL,
                "Value" BLOB NULL,
                "UploadDate" datetime NOT NULL,
                "UploadedBy" TEXT NULL,
                "UserId" TEXT NULL,
                "SourceFileId" TEXT NULL,
                "TenantId" TEXT NOT NULL,
                "LastModified" datetime NOT NULL,
                "LastModifiedBy" TEXT NULL,
                "Deleted" INTEGER NOT NULL,
                "DeletedAt" datetime NULL,
                CONSTRAINT "IX_FileStorage_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId")
            )
            """);


        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS "UserInGroups" (
                "UserInGroupId" TEXT NOT NULL CONSTRAINT "PK_UserInGroups" PRIMARY KEY,
                "UserId" TEXT NOT NULL,
                "TenantId" TEXT NOT NULL,
                "GroupId" TEXT NOT NULL,
                CONSTRAINT "FK_UserInGroups_UserGroups" FOREIGN KEY ("GroupId") REFERENCES "UserGroups" ("GroupId"),
                CONSTRAINT "FK_UserInGroups_Users" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId")
            );
            """);




        m1.Add(
            """
            CREATE INDEX "IX_FileStorage_UserId" ON "FileStorage" ("UserId");
            """);



        // {{ModuleItemStart:Tags}}
        m1.Add(
            """
            CREATE INDEX "IX_TagItems_TagId" ON "TagItems" ("TagId");
            """);
        // {{ModuleItemEnd:Tags}}

        m1.Add(
            """
            CREATE INDEX "IX_UserInGroups_GroupId" ON "UserInGroups" ("GroupId");
            """);

        m1.Add(
            """
            CREATE INDEX "IX_UserInGroups_UserId" ON "UserInGroups" ("UserId");
            """);

        m1.Add(
            """
            CREATE INDEX "IX_Users_DepartmentId" ON "Users" ("DepartmentId");
            """);

        m1.Add(
            """
            CREATE INDEX "IX_Users_TenantId" ON "Users" ("TenantId");
            """);

        m1.Add(
            """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('001', '1.0.0')
            EXCEPT
            SELECT * FROM __EFMigrationsHistory WHERE MigrationId='001'
            """);

        output.Add(new DataObjects.DataMigration {
            MigrationId = "001",
            Migration = m1
        });

        return output;
    }
}
