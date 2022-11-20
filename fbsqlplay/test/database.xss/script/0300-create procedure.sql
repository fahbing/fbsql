
SET TIMEOUT 0

GO

IF (OBJECT_ID(N'[dbo].[p_timeout]', 'P') IS NULL) 
  EXEC (N'CREATE PROCEDURE dbo.p_timeout AS SET NOCOUNT ON')

GO

ALTER PROCEDURE dbo.p_timeout
AS
BEGIN
  SET NOCOUNT ON
END;

GO

EXEC dbo.p_timeout

GO