-- <step name="create database" executable="true" guid="F6FBE5D1-0598-462D-A642-B74DF8E6CDE9" />

USE master;

GO

SET language "us_english";

IF ( (2 & @@OPTIONS) = 2 ) 
  PRINT N'IMPLICIT_TRANSACTIONS = ''ON'''
ELSE 
  PRINT N'IMPLICIT_TRANSACTIONS = ''OFF''';  
GO

COMMIT 

GO

STOPTRANSACTION;

GO

IF (NOT EXISTS(SELECT *
                 FROM sys.databases 
                WHERE name = N'_dbName_'))
  CREATE DATABASE _dbName_;

GO

STARTTRANSACTION;

GO

USE _dbName_;

GO