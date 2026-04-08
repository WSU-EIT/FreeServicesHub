namespace FreeCICD;
public partial class DataMigrations
{
    public List<DataObjects.DataMigration> GetMigrationsMySQL()
    {
        List<DataObjects.DataMigration> output = new List<DataObjects.DataMigration>();

        List<string> m1 = new List<string>();
        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
                `MigrationId` varchar(150) NOT NULL,
                `ProductVersion` varchar(32) NOT NULL,
                PRIMARY KEY (`MigrationId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);

        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `DepartmentGroups` (
                `DepartmentGroupId` char(36) NOT NULL,
                `DepartmentGroupName` varchar(200) NULL,
                `TenantId` char(36) NOT NULL,
                `Added` datetime NOT NULL,
                `AddedBy` varchar(100) NULL,
                `LastModified` datetime NOT NULL,
                `LastModifiedBy` varchar(100) NULL,
                `Deleted` tinyint(1) NOT NULL,
                `DeletedAt` datetime NULL,
                PRIMARY KEY (`DepartmentGroupId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);

        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `Departments` (
                `DepartmentId` char(36) NOT NULL,
                `DepartmentName` varchar(100) NOT NULL,
                `ActiveDirectoryNames` varchar(100) NULL,
                `Enabled` tinyint(1) NOT NULL,
                `DepartmentGroupId` char(36) NULL,
                `TenantId` char(36) NOT NULL,
                `Added` datetime NOT NULL,
                `AddedBy` varchar(100) NULL,
                `LastModified` datetime NOT NULL,
                `LastModifiedBy` varchar(100) NULL,
                `Deleted` tinyint(1) NOT NULL,
                `DeletedAt` datetime NULL,
                PRIMARY KEY (`DepartmentId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);



        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `PluginCache` (
                `RecordId` char(36) NOT NULL,
                `Id` char(36) NOT NULL,
                `Author` varchar(100) NULL,
                `Name` varchar(100) NULL,
                `Type` varchar(100) NULL,
                `Version` varchar(100) NULL,
                `Properties` longtext NULL,
                `Namespace` varchar(100) NULL,
                `ClassName` varchar(100) NULL,
                `Code` longtext NULL,
                `AdditionalAssemblies` longtext NULL,
                `StillExists` tinyint(1) NOT NULL,
                PRIMARY KEY (`RecordId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);


        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `Settings` (
                `SettingId` int NOT NULL AUTO_INCREMENT,
                `SettingName` varchar(100) NOT NULL,
                `SettingType` varchar(100) NULL,
                `SettingNotes` longtext NULL,
                `SettingText` longtext NULL,
                `TenantId` char(36) NULL,
                `UserId` char(36) NULL,
                `LastModified` datetime NOT NULL,
                `LastModifiedBy` varchar(100) NULL,
                PRIMARY KEY (`SettingId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);

        // {{ModuleItemStart:Tags}}
        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `Tags` (
                `TagId` char(36) NOT NULL,
                `TenantId` char(36) NOT NULL,
                `Name` varchar(200) NOT NULL,
                `Style` longtext NULL,
                `Enabled` tinyint(1) NOT NULL,
                `Added` datetime NOT NULL,
                `AddedBy` varchar(100) NULL,
                `LastModified` datetime NOT NULL,
                `LastModifiedBy` varchar(100) NULL,
                `Deleted` tinyint(1) NOT NULL,
                `DeletedAt` datetime NULL,
                PRIMARY KEY (`TagId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);
        // {{ModuleItemEnd:Tags}}

        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `Tenants` (
                `TenantId` char(36) NOT NULL,
                `Name` varchar(200) NOT NULL,
                `TenantCode` varchar(50) NOT NULL,
                `Enabled` tinyint(1) NOT NULL,
                `Added` datetime NOT NULL,
                `AddedBy` varchar(100) NULL,
                `LastModified` datetime NOT NULL,
                `LastModifiedBy` varchar(100) NULL,
                PRIMARY KEY (`TenantId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);

        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `UDFLabels` (
                `Id` char(36) NOT NULL,
                `Module` varchar(20) NOT NULL,
                `UDF` varchar(10) NOT NULL,
                `Label` longtext NULL,
                `ShowColumn` tinyint(1) NULL,
                `ShowInFilter` tinyint(1) NULL,
                `IncludeInSearch` tinyint(1) NULL,
                `TenantId` char(36) NOT NULL,
                `LastModified` datetime NOT NULL,
                `LastModifiedBy` varchar(100) NULL,
                PRIMARY KEY (`Id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);

        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `UserGroups` (
                `GroupId` char(36) NOT NULL,
                `TenantId` char(36) NOT NULL,
                `Name` varchar(100) NOT NULL,
                `Enabled` tinyint(1) NOT NULL,
                `Settings` longtext NULL,
                `Added` datetime NOT NULL,
                `AddedBy` varchar(100) NULL,
                `LastModified` datetime NOT NULL,
                `LastModifiedBy` varchar(100) NULL,
                `Deleted` tinyint(1) NOT NULL,
                `DeletedAt` datetime NULL,
                PRIMARY KEY (`GroupId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);


        // {{ModuleItemStart:Tags}}
        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `TagItems` (
                `TagItemId` char(36) NOT NULL,
                `TagId` char(36) NOT NULL,
                `TenantId` char(36) NOT NULL,
                `ItemId` char(36) NOT NULL,
                PRIMARY KEY (`TagItemId`),
                CONSTRAINT `FK_TagItems_Tags` FOREIGN KEY (`TagId`) REFERENCES `Tags` (`TagId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);
        // {{ModuleItemEnd:Tags}}

        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `Users` (
                `UserId` char(36) NOT NULL,
                `TenantId` char(36) NOT NULL,
                `FirstName` varchar(100) NULL,
                `LastName` varchar(100) NULL,
                `Email` varchar(100) NOT NULL,
                `Phone` varchar(20) NULL,
                `Username` varchar(100) NOT NULL,
                `EmployeeId` varchar(50) NULL,
                `DepartmentId` char(36) NULL,
                `Title` varchar(255) NULL,
                `Location` varchar(255) NULL,
                `Enabled` tinyint(1) NOT NULL,
                `LastLogin` datetime NULL,
                `LastLoginSource` varchar(50) NULL,
                `Admin` tinyint(1) NOT NULL,
                `CanBeScheduled` tinyint(1) NOT NULL,
                `ManageFiles` tinyint(1) NOT NULL,
                `ManageAppointments` tinyint(1) NOT NULL,
                `Password` longtext NULL,
                `PreventPasswordChange` tinyint(1) NOT NULL,
                `FailedLoginAttempts` int NULL,
                `LastLockoutDate` datetime NULL,
                `Source` varchar(100) NULL,
                `UDF01` varchar(500) NULL,
                `UDF02` varchar(500) NULL,
                `UDF03` varchar(500) NULL,
                `UDF04` varchar(500) NULL,
                `UDF05` varchar(500) NULL,
                `UDF06` varchar(500) NULL,
                `UDF07` varchar(500) NULL,
                `UDF08` varchar(500) NULL,
                `UDF09` varchar(500) NULL,
                `UDF10` varchar(500) NULL,
                `Added` datetime NOT NULL,
                `AddedBy` varchar(100) NULL,
                `LastModified` datetime NOT NULL,
                `LastModifiedBy` varchar(100) NULL,
                `Deleted` tinyint(1) NOT NULL,
                `DeletedAt` datetime NULL,
                `Preferences` longtext NULL,
                PRIMARY KEY (`UserId`),
                CONSTRAINT `IX_Users_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`DepartmentId`),
                CONSTRAINT `IX_Users_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`TenantId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);


        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `FileStorage` (
                `FileId` char(36) NOT NULL,
                `ItemId` char(36) NULL,
                `FileName` varchar(255) NOT NULL,
                `Extension` varchar(15) NOT NULL,
                `Bytes` bigint NULL,
                `Value` longblob NULL,
                `UploadDate` datetime NOT NULL,
                `UploadedBy` varchar(100) NULL,
                `UserId` char(36) NULL,
                `SourceFileId` varchar(100) NULL,
                `TenantId` char(36) NOT NULL,
                `LastModified` datetime NOT NULL,
                `LastModifiedBy` varchar(100) NULL,
                `Deleted` tinyint(1) NOT NULL,
                `DeletedAt` datetime NULL,
                PRIMARY KEY (`FileId`),
                CONSTRAINT `IX_FileStorage_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`UserId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);


        m1.Add(
            """
            CREATE TABLE IF NOT EXISTS `UserInGroups` (
                `UserInGroupId` char(36) NOT NULL,
                `UserId` char(36) NOT NULL,
                `TenantId` char(36) NOT NULL,
                `GroupId` char(36) NOT NULL,
                PRIMARY KEY (`UserInGroupId`),
                CONSTRAINT `FK_UserInGroups_UserGroups` FOREIGN KEY (`GroupId`) REFERENCES `UserGroups` (`GroupId`),
                CONSTRAINT `FK_UserInGroups_Users` FOREIGN KEY (`UserId`) REFERENCES `Users` (`UserId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """);




        m1.Add(
            """
            CREATE INDEX `IX_FileStorage_UserId` ON `FileStorage` (`UserId`);
            """);



        // {{ModuleItemStart:Tags}}
        m1.Add(
            """
            CREATE INDEX `IX_TagItems_TagId` ON `TagItems` (`TagId`);
            """);
        // {{ModuleItemEnd:Tags}}

        m1.Add(
            """
            CREATE INDEX `IX_UserInGroups_GroupId` ON `UserInGroups` (`GroupId`);
            """);

        m1.Add(
            """
            CREATE INDEX `IX_UserInGroups_UserId` ON `UserInGroups` (`UserId`);
            """);

        m1.Add(
            """
            CREATE INDEX `IX_Users_DepartmentId` ON `Users` (`DepartmentId`);
            """);

        m1.Add(
            """
            CREATE INDEX `IX_Users_TenantId` ON `Users` (`TenantId`);
            """);

        m1.Add(
            """
            INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
            VALUES ('001', '1.0.0');
            """);


        output.Add(new DataObjects.DataMigration {
            MigrationId = "001",
            Migration = m1
        });

        return output;
    }
}
