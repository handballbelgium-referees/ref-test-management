-- Run in the environment database with the configured Microsoft Entra admin.
-- Supply the identity names, object IDs, and each object's first five ID characters
-- from the matching Terraform outputs.

IF DATABASE_PRINCIPAL_ID(N'$(ApiIdentityName)_$(ApiObjectIdSuffix)') IS NULL
BEGIN
    CREATE USER [$(ApiIdentityName)_$(ApiObjectIdSuffix)]
        FROM EXTERNAL PROVIDER
        WITH OBJECT_ID = '$(ApiObjectId)';
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS members
    INNER JOIN sys.database_principals AS roles
        ON roles.principal_id = members.role_principal_id
    INNER JOIN sys.database_principals AS users
        ON users.principal_id = members.member_principal_id
    WHERE roles.name = N'db_datareader'
      AND users.name = N'$(ApiIdentityName)_$(ApiObjectIdSuffix)'
)
    ALTER ROLE [db_datareader] ADD MEMBER [$(ApiIdentityName)_$(ApiObjectIdSuffix)];

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS members
    INNER JOIN sys.database_principals AS roles
        ON roles.principal_id = members.role_principal_id
    INNER JOIN sys.database_principals AS users
        ON users.principal_id = members.member_principal_id
    WHERE roles.name = N'db_datawriter'
      AND users.name = N'$(ApiIdentityName)_$(ApiObjectIdSuffix)'
)
    ALTER ROLE [db_datawriter] ADD MEMBER [$(ApiIdentityName)_$(ApiObjectIdSuffix)];

IF DATABASE_PRINCIPAL_ID(N'$(WorkerIdentityName)_$(WorkerObjectIdSuffix)') IS NULL
BEGIN
    CREATE USER [$(WorkerIdentityName)_$(WorkerObjectIdSuffix)]
        FROM EXTERNAL PROVIDER
        WITH OBJECT_ID = '$(WorkerObjectId)';
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS members
    INNER JOIN sys.database_principals AS roles
        ON roles.principal_id = members.role_principal_id
    INNER JOIN sys.database_principals AS users
        ON users.principal_id = members.member_principal_id
    WHERE roles.name = N'db_datareader'
      AND users.name = N'$(WorkerIdentityName)_$(WorkerObjectIdSuffix)'
)
    ALTER ROLE [db_datareader] ADD MEMBER [$(WorkerIdentityName)_$(WorkerObjectIdSuffix)];

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS members
    INNER JOIN sys.database_principals AS roles
        ON roles.principal_id = members.role_principal_id
    INNER JOIN sys.database_principals AS users
        ON users.principal_id = members.member_principal_id
    WHERE roles.name = N'db_datawriter'
      AND users.name = N'$(WorkerIdentityName)_$(WorkerObjectIdSuffix)'
)
    ALTER ROLE [db_datawriter] ADD MEMBER [$(WorkerIdentityName)_$(WorkerObjectIdSuffix)];

IF DATABASE_PRINCIPAL_ID(N'$(MigrationIdentityName)_$(MigrationObjectIdSuffix)') IS NULL
BEGIN
    CREATE USER [$(MigrationIdentityName)_$(MigrationObjectIdSuffix)]
        FROM EXTERNAL PROVIDER
        WITH OBJECT_ID = '$(MigrationObjectId)';
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS members
    INNER JOIN sys.database_principals AS roles
        ON roles.principal_id = members.role_principal_id
    INNER JOIN sys.database_principals AS users
        ON users.principal_id = members.member_principal_id
    WHERE roles.name = N'db_owner'
      AND users.name = N'$(MigrationIdentityName)_$(MigrationObjectIdSuffix)'
)
    ALTER ROLE [db_owner] ADD MEMBER [$(MigrationIdentityName)_$(MigrationObjectIdSuffix)];
