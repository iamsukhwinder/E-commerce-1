/*
  Optional SQL Server bootstrap script.
  The API creates the tables and seed data on its first start. This script only
  creates the local database ahead of time and is safe to run more than once.

  Example:
  sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i .\api\database\00-create-database.sql
*/
IF DB_ID(N'EcommerceLocal') IS NULL
BEGIN
    CREATE DATABASE [EcommerceLocal];
END
GO

ALTER DATABASE [EcommerceLocal] SET READ_COMMITTED_SNAPSHOT ON;
GO
