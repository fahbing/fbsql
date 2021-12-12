
IF (OBJECT_ID('tempDB..##xssMonitor') IS NOT NULL)
  DROP PROCEDURE ##xssMonitor

GO

CREATE PROCEDURE ##xssMonitor
AS
BEGIN
  SET NOCOUNT ON

  DECLARE
    @free  DECIMAL(12, 2),
    @msg   NVARCHAR(MAX) = CONCAT(N'mon: ', CONVERT(NVARCHAR(64), SYSDATETIMEOFFSET(), 126)),
    @total DECIMAL(12, 2),
    @used  DECIMAL(12, 2)

  SELECT @total = ROUND(total_log_size_in_bytes * 1.0 / 1024 / 1024, 0),
         @used  = ROUND(used_log_space_in_bytes * 1.0 / 1024 / 1024, 0)
    FROM _dbName_.sys.dm_db_log_space_usage

  SET @msg = CONCAT(@msg, CHAR(13), CHAR(10), N'_dbName_ log space in MB: total '
                  , @total, N', used ', @used, N', free ', @total - @used)

  SELECT @total = CONVERT(DECIMAL(12, 2), ROUND(SUM(total_page_count) / 128., 2)),
         @used  = CONVERT(DECIMAL(12, 2), ROUND(SUM(allocated_extent_page_count) / 128., 2))
    FROM _dbName_.sys.dm_db_file_space_usage

  SET @msg = CONCAT(@msg, CHAR(13), CHAR(10), N'_dbName_ file space in MB: total '
                  , @total, N', used ', @used, N', free ', @total - @used)

  SELECT @total = ROUND(total_log_size_in_bytes * 1.0 / 1024 / 1024, 0),
         @used  = ROUND(used_log_space_in_bytes * 1.0 / 1024 / 1024, 0)
    FROM tempdb.sys.dm_db_log_space_usage

  SET @msg = CONCAT(@msg, CHAR(13), CHAR(10), N'tempdb log space in MB: total '
                  , @total, N', used ', @used, N', free ', @total - @used)

  SELECT @total = CONVERT(DECIMAL(12, 2), ROUND(SUM(total_page_count) / 128., 2)),
         @used  = CONVERT(DECIMAL(12, 2), ROUND(SUM(allocated_extent_page_count) / 128., 2))
    FROM tempdb.sys.dm_db_file_space_usage

  SET @msg = CONCAT(@msg, CHAR(13), CHAR(10), N'tempdb file space in MB: total '
                  , @total, N', used ', @used, N', free ', @total - @used)

  PRINT @msg
END

GO

EXEC ##xssMonitor

GO