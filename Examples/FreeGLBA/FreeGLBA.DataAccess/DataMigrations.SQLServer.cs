namespace FreeGLBA;
public partial class DataMigrations
{
    public List<DataObjects.DataMigration> GetMigrationsSqlServer()
    {
        List<DataObjects.DataMigration> output = new List<DataObjects.DataMigration>();

        List<string> m1 = new List<string>();
        m1.Add(
            """
            IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
            BEGIN
                CREATE TABLE [__EFMigrationsHistory] (
                    [MigrationId] nvarchar(150) NOT NULL,
                    [ProductVersion] nvarchar(32) NOT NULL,
                    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                )
            END
            """);


        m1.Add(
            """
            IF OBJECT_ID(N'[DepartmentGroups]') IS NULL
            BEGIN
                CREATE TABLE [dbo].[DepartmentGroups](
                    [DepartmentGroupId] [uniqueidentifier] NOT NULL,
                    [DepartmentGroupName] [nvarchar](200) NULL,
                    [TenantId] [uniqueidentifier] NOT NULL,
                    [Added] [datetime] NOT NULL,
                    [AddedBy] [nvarchar](100) NULL,
                    [LastModified] [datetime] NOT NULL,
                    [LastModifiedBy] [nvarchar](100) NULL,
                    [Deleted] [bit] NOT NULL,
                    [DeletedAt] [datetime] NULL,
                    CONSTRAINT [PK_DepartmentGroups] PRIMARY KEY CLUSTERED ([DepartmentGroupId] ASC)
                    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                ) ON [PRIMARY]
            END
            """);

        m1.Add(
            """
            IF OBJECT_ID(N'[Departments]') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Departments](
                    [DepartmentId] [uniqueidentifier] NOT NULL,
                    [DepartmentName] [nvarchar](100) NOT NULL,
                    [ActiveDirectoryNames] [nvarchar](100) NULL,
                    [Enabled] [bit] NOT NULL,
                    [DepartmentGroupId] [uniqueidentifier] NULL,
                    [TenantId] [uniqueidentifier] NOT NULL,
                    [Added] [datetime] NOT NULL,
                    [AddedBy] [nvarchar](100) NULL,
                    [LastModified] [datetime] NOT NULL,
                    [LastModifiedBy] [nvarchar](100) NULL,
                    [Deleted] [bit] NOT NULL,
                    [DeletedAt] [datetime] NULL,
                    CONSTRAINT [PK_Departments] PRIMARY KEY CLUSTERED ([DepartmentId] ASC)
                    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                ) ON [PRIMARY]
            END
            """);


        m1.Add(
            """
            IF OBJECT_ID(N'[FileStorage]') IS NULL
            BEGIN
                CREATE TABLE [dbo].[FileStorage](
                    [FileId] [uniqueidentifier] NOT NULL,
                    [ItemId] [uniqueidentifier] NULL,
                    [FileName] [nvarchar](255) NOT NULL,
                    [Extension] [nvarchar](15) NOT NULL,
                    [Bytes] [bigint] NULL,
                    [Value] [varbinary](max) NULL,
                    [UploadDate] [datetime] NOT NULL,
                    [UploadedBy] [nvarchar](100) NULL,
                    [UserId] [uniqueidentifier] NULL,
                    [SourceFileId] [nvarchar](100) NULL,
                    [TenantId] [uniqueidentifier] NOT NULL,
                    [LastModified] [datetime] NOT NULL,
                    [LastModifiedBy] [nvarchar](100) NULL,
                    [Deleted] [bit] NOT NULL,
                    [DeletedAt] [datetime] NULL,
                    CONSTRAINT [PK_FileStorage] PRIMARY KEY CLUSTERED ([FileId] ASC)
                    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
            END
            """);




        m1.Add(
            """
            IF OBJECT_ID(N'[PluginCache]') IS NULL
            BEGIN
                CREATE TABLE [dbo].[PluginCache](
                    [RecordId] [uniqueidentifier] NOT NULL,
                    [Id] [uniqueidentifier] NOT NULL,
                    [Author] [nvarchar](100) NULL,
                    [Name] [nvarchar](100) NULL,
                    [Type] [nvarchar](100) NULL,
                    [Version] [nvarchar](100) NULL,
                    [Properties] [nvarchar](max) NULL,
                    [Namespace] [nvarchar](100) NULL,
                    [ClassName] [nvarchar](100) NULL,
                    [Code] [nvarchar](max) NULL,
                    [AdditionalAssemblies] [nvarchar](max) NULL,
                    [StillExists] [bit] NOT NULL,
                    CONSTRAINT [PK_PluginCache] PRIMARY KEY CLUSTERED ([RecordId] ASC)
                    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
            END
            """);


        m1.Add(
            """
            IF OBJECT_ID(N'[Settings]') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Settings](
                    [SettingId] [int] IDENTITY(1,1) NOT NULL,
                    [SettingName] [nvarchar](100) NOT NULL,
                    [SettingType] [nvarchar](100) NULL,
                    [SettingNotes] [nvarchar](max) NULL,
                    [SettingText] [nvarchar](max) NULL,
                    [TenantId] [uniqueidentifier] NULL,
                    [UserId] [uniqueidentifier] NULL,
                    [LastModified] [datetime] NOT NULL,
                    [LastModifiedBy] [nvarchar](100) NULL,
                    CONSTRAINT [PK_Settings_1] PRIMARY KEY CLUSTERED ([SettingId] ASC)
                    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
            END
            """);

        // {{ModuleItemStart:Tags}}
        m1.Add(
            """
            IF OBJECT_ID(N'[TagItems]') IS NULL
            BEGIN
                CREATE TABLE [dbo].[TagItems](
                    [TagItemId] [uniqueidentifier] NOT NULL,
                    [TagId] [uniqueidentifier] NOT NULL,
                    [TenantId] [uniqueidentifier] NOT NULL,
                    [ItemId] [uniqueidentifier] NOT NULL,
                    CONSTRAINT [PK_TagItems] PRIMARY KEY CLUSTERED ([TagItemId] ASC)
                    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                ) ON [PRIMARY]
            END
            """);

        m1.Add(
            """
            IF OBJECT_ID(N'[Tags]') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Tags](
                    [TagId] [uniqueidentifier] NOT NULL,
                    [TenantId] [uniqueidentifier] NOT NULL,
                    [Name] [nvarchar](200) NOT NULL,
                    [Style] [nvarchar](max) NULL,
                    [Enabled] [bit] NOT NULL,
                    [UseInAppointments] [bit] NOT NULL,
                    [UseInEmailTemplates] [bit] NOT NULL,
                    [UseInServices] [bit] NOT NULL,
                    [Added] [datetime] NOT NULL,
                    [AddedBy] [nvarchar](100) NULL,
                    [LastModified] [datetime] NOT NULL,
                    [LastModifiedBy] [nvarchar](100) NULL,
                    [Deleted] [bit] NOT NULL,
                    [DeletedAt] [datetime] NULL,
                    CONSTRAINT [PK_Tags] PRIMARY KEY CLUSTERED ([TagId] ASC)
                    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
            END
            """);
        // {{ModuleItemEnd:Tags}}

        m1.Add(
            """
            IF OBJECT_ID(N'[Tenants]') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Tenants](
                    [TenantId] [uniqueidentifier] NOT NULL,
                    [Name] [nvarchar](200) NOT NULL,
                    [TenantCode] [nvarchar](50) NOT NULL,
                    [Enabled] [bit] NOT NULL,
                    [Added] [datetime] NOT NULL,
                    [AddedBy] [nvarchar](100) NULL,
                    [LastModified] [datetime] NOT NULL,
                    [LastModifiedBy] [nvarchar](100) NULL,
                    CONSTRAINT [PK_Tenants] PRIMARY KEY CLUSTERED ([TenantId] ASC)
                    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                ) ON [PRIMARY]
            END
            """);

        m1.Add(
            """
            IF OBJECT_ID(N'[UDFLabels]') IS NULL
            BEGIN
                CREATE TABLE [dbo].[UDFLabels](
                    [Id] [uniqueidentifier] NOT NULL,
                    [Module] [nvarchar](20) NOT NULL,
                    [UDF] [nvarchar](10) NOT NULL,
                    [Label] [nvarchar](max) NULL,
                    [ShowColumn] [bit] NULL,
                    [ShowInFilter] [bit] NULL,
                    [IncludeInSearch] [bit] NULL,
                    [TenantId] [uniqueidentifier] NOT NULL,
                    [LastModified] [datetime] NOT NULL,
                    [LastModifiedBy] [nvarchar](100) NULL,
                    CONSTRAINT [PK_UDFLabels] PRIMARY KEY CLUSTERED ([Id] ASC)
                    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
            END
            """);

        m1.Add(
            """
            IF OBJECT_ID(N'[UserGroups]') IS NULL
            BEGIN
                CREATE TABLE [dbo].[UserGroups](
                    [GroupId] [uniqueidentifier] NOT NULL,
                    [TenantId] [uniqueidentifier] NOT NULL,
                    [Name] [nvarchar](100) NOT NULL,
                    [Enabled] [bit] NOT NULL,
                    [Settings] [nvarchar](max) NULL,
                    [Added] [datetime] NOT NULL,
                    [AddedBy] [nvarchar](100) NULL,
                    [LastModified] [datetime] NOT NULL,
                    [LastModifiedBy] [nvarchar](100) NULL,
                    [Deleted] [bit] NOT NULL,
                    [DeletedAt] [datetime] NULL,
                    CONSTRAINT [PK_UserGroups] PRIMARY KEY CLUSTERED ([GroupId] ASC)
                    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
            END
            """);

        m1.Add(
            """
            IF OBJECT_ID(N'[UserInGroups]') IS NULL
            BEGIN
                CREATE TABLE [dbo].[UserInGroups](
                    [UserInGroupId] [uniqueidentifier] NOT NULL,
                    [UserId] [uniqueidentifier] NOT NULL,
                    [TenantId] [uniqueidentifier] NOT NULL,
                    [GroupId] [uniqueidentifier] NOT NULL,
                    CONSTRAINT [PK_UserInGroups] PRIMARY KEY CLUSTERED ([UserInGroupId] ASC)
                    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                ) ON [PRIMARY]
            END
            """);

        var usersTable =
            """
            IF OBJECT_ID(N'[Users]') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Users](
                    [UserId] [uniqueidentifier] NOT NULL,
                    [TenantId] [uniqueidentifier] NOT NULL,
                    [FirstName] [nvarchar](100) NULL,
                    [LastName] [nvarchar](100) NULL,
                    [Email] [nvarchar](100) NOT NULL,
                    [Phone] [nvarchar](20) NULL,
                    [Username] [nvarchar](100) NOT NULL,
                    [EmployeeId] [nvarchar](50) NULL,
                    [DepartmentId] [uniqueidentifier] NULL,
                    [Title] [nvarchar](255) NULL,
                    [Location] [nvarchar](255) NULL,
                    [Enabled] [bit] NOT NULL,
                    [LastLogin] [datetime] NULL,
                    [LastLoginSource] [nvarchar](50) NULL,
                    [Admin] [bit] NOT NULL,
            
            """;


        usersTable +=
            """
                    [ManageFiles] [bit] NOT NULL,
                    [Password] [nvarchar](max) NULL,
                    [PreventPasswordChange] [bit] NOT NULL,
                    [FailedLoginAttempts] [int] NULL,
                    [LastLockoutDate] [datetime] NULL,
                    [Source] [nvarchar](100) NULL,
                    [UDF01] [nvarchar](500) NULL,
                    [UDF02] [nvarchar](500) NULL,
                    [UDF03] [nvarchar](500) NULL,
                    [UDF04] [nvarchar](500) NULL,
                    [UDF05] [nvarchar](500) NULL,
                    [UDF06] [nvarchar](500) NULL,
                    [UDF07] [nvarchar](500) NULL,
                    [UDF08] [nvarchar](500) NULL,
                    [UDF09] [nvarchar](500) NULL,
                    [UDF10] [nvarchar](500) NULL,
                    [Added] [datetime] NOT NULL,
                    [AddedBy] [nvarchar](100) NULL,
                    [LastModified] [datetime] NOT NULL,
                    [LastModifiedBy] [nvarchar](100) NULL,
                    [Deleted] [bit] NOT NULL,
                    [DeletedAt] [datetime] NULL,
                    [Preferences] [nvarchar](max) NULL,
                    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserId] ASC)
                    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
            END
            """;

        m1.Add(usersTable);


        m1.Add(
            """
            IF NOT EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME='IX_FileStorage_UserId')
            BEGIN
            	ALTER TABLE[dbo].[FileStorage]  WITH CHECK ADD CONSTRAINT[IX_FileStorage_UserId] FOREIGN KEY([UserId])
                REFERENCES[dbo].[Users]([UserId])
                ALTER TABLE[dbo].[FileStorage] CHECK CONSTRAINT[IX_FileStorage_UserId]
            END
            """);



        // {{ModuleItemStart:Tags}}
        m1.Add(
            """
            IF NOT EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME='FK_TagItems_Tags')
            BEGIN
            	ALTER TABLE [dbo].[TagItems]  WITH CHECK ADD  CONSTRAINT [FK_TagItems_Tags] FOREIGN KEY([TagId])
                REFERENCES [dbo].[Tags] ([TagId])
                ALTER TABLE [dbo].[TagItems] CHECK CONSTRAINT [FK_TagItems_Tags]
            END
            """);
        // {{ModuleItemEnd:Tags}}

        m1.Add(
            """
            IF NOT EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME='FK_UserInGroups_UserGroups')
            BEGIN
            	ALTER TABLE [dbo].[UserInGroups]  WITH CHECK ADD  CONSTRAINT [FK_UserInGroups_UserGroups] FOREIGN KEY([GroupId])
                REFERENCES [dbo].[UserGroups] ([GroupId])
                ALTER TABLE [dbo].[UserInGroups] CHECK CONSTRAINT [FK_UserInGroups_UserGroups]
            END
            """);

        m1.Add(
            """
            IF NOT EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME='FK_UserInGroups_Users')
            BEGIN
            	ALTER TABLE [dbo].[UserInGroups]  WITH CHECK ADD  CONSTRAINT [FK_UserInGroups_Users] FOREIGN KEY([UserId])
                REFERENCES [dbo].[Users] ([UserId])
                ALTER TABLE [dbo].[UserInGroups] CHECK CONSTRAINT [FK_UserInGroups_Users]
            END
            """);

        m1.Add(
            """
            IF NOT EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME='IX_Users_DepartmentId')
            BEGIN
            	ALTER TABLE [dbo].[Users]  WITH CHECK ADD  CONSTRAINT [IX_Users_DepartmentId] FOREIGN KEY([DepartmentId])
                REFERENCES [dbo].[Departments] ([DepartmentId])
                ALTER TABLE [dbo].[Users] CHECK CONSTRAINT [IX_Users_DepartmentId]
            END
            """);

        m1.Add(
            """
            IF NOT EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME='IX_Users_TenantId')
            BEGIN
                ALTER TABLE [dbo].[Users]  WITH CHECK ADD  CONSTRAINT [IX_Users_TenantId] FOREIGN KEY([TenantId])
                REFERENCES [dbo].[Tenants] ([TenantId])
                ALTER TABLE [dbo].[Users] CHECK CONSTRAINT [IX_Users_TenantId]
            END
            """);

        m1.Add(
            """
            IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NOT NULL
            BEGIN
            	IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId]=N'001')
            	BEGIN
            		INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
            		VALUES (N'001', N'1.0.0')
            	END
            END
            """);

        output.Add(new DataObjects.DataMigration {
            MigrationId = "001",
            Migration = m1
        });

        return output;
    }
}
