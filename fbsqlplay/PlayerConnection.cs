using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.SqlClient;


/// 2021-12-12 - created

namespace Fahbing.Sql
{
  internal class SqlException : DbException
  {
    public byte Class { get; private set; }

    public int LineNumber { get; private set; }

    public int Number { get; private set; }

    public string Procedure { get; private set; }

    public byte State { get; private set; }

    public SqlException(SqlError error) 
         : base(error.Message) 
    { 
      Class = error.Class;
      LineNumber = error.LineNumber; 
      Number = error.Number;
      Procedure = error.Procedure;
      State = error.State;
    }
  }

  /// <summary>
  /// A connection object for the <see cref="SqlScriptPlayer"/> that implements 
  /// the system.data.sqlclient and system.data.oledb connection provider.
  /// </summary>
  internal class PlayerConnection : SqlScriptConnection
  {
    private const string ResErrCompLevel = "An error occurred while determining the compatibility level.";
    private const string ResNoConnection = "No connection is active.";
    private const string ResNoTransaction = "No transaction is active.";
    private const string ResServerSideRollback = "The transaction was terminated on the server side. Check if the rollback was done properly!";

    private DbCommand Command;
    private DbConnection Connection;
    private int Timeout = 0;
    private DbTransaction Transaction;

    /// <summary>
    /// Starts a new transaction.
    /// </summary>
    public override void BeginTransaction()
    {
      Transaction = Connection.BeginTransaction();
    }

    /// <summary>
    /// Executes a commit and finished the running transaction.
    /// </summary>
    /// <exception cref="ApplicationException">Throws an exception when no 
    /// transaction is active.</exception>
    /// <remarks>Transaction.Commit() and Transaction.Rollback() used the 
    /// connection timeout (default is 15 seconds). In large scripts with 
    /// many changes, this has resulted in a timeout error during commit.
    /// </remarks>
    public override void Commit()
    {
      if (Transaction == null)
        throw new ApplicationException(ResNoTransaction);

      if (Connection is SqlConnection)
      {
        Command.CommandText = "COMMIT";
        Command.CommandTimeout = Timeout;
        Command.Transaction = Transaction;

        Command.ExecuteNonQuery();
      }
      else
        Transaction.Commit();

      Transaction = null;
    }

    /// <summary>
    /// Opens the connection to the database.
    /// </summary>
    /// <param name="connectionString">A connection string to establish the 
    /// connection.</param>
    /// <exception cref="ApplicationException"></exception>
    public override void Connect(string connectionString)
    {
      var parameters = ExtractProvider(connectionString);

      Connection = (parameters[0]) switch
      {
        "System.Data.SqlClient" => new SqlConnection(),
        "System.Data.Odbc" => new OdbcConnection(),
        _ => new OleDbConnection()
      };

      if (Connection is SqlConnection sqlConnection)
      {
        SqlConnectionStringBuilder builder = new(parameters[1])
        {
          ApplicationName = "FBSQL Command Line Script Player"
        };
        Connection.ConnectionString = builder.ConnectionString;
        sqlConnection.FireInfoMessageEventOnUserErrors = true;

        sqlConnection.InfoMessage += (object sender
        , SqlInfoMessageEventArgs e) =>
        {
          if (e.Errors[0].Class < 11)
          {
            foreach (SqlError error in e.Errors)
            {
              OnMessage?.Invoke(e.Message);
            }
          } else
          {
            OnException?.Invoke(CreateException(e.Errors));
          }
        };
      }
      else if (Connection is OdbcConnection odbcConnection)
      {
        OdbcConnectionStringBuilder builder = new(parameters[1]);
        odbcConnection.ConnectionString = builder.ConnectionString;
      }
      else
      {
        OleDbConnectionStringBuilder builder = new(parameters[1])
        {
          Provider = parameters[0]
        };

        Connection.ConnectionString = builder.ConnectionString;
      }

      Connection.Open();

      Command = Connection.CreateCommand();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="errors"></param>
    /// <returns></returns>
    internal static Exception CreateException(SqlErrorCollection errors) 
    { 
      if (errors.Count > 1)
      {
        var exceptions = new Collection<Exception>();

        foreach (SqlError error in errors)
        {
          exceptions.Add(new SqlException(error));
        }
        return new AggregateException(exceptions);

      } else
      {
        return new SqlException(errors[0]);
      }
    }

    /// <summary>
    /// Closes the connection to the database.
    /// </summary>
    public override void Disconnect()
    {
      if (Transaction != null)
      {
        Transaction.Rollback();

        Transaction = null;
      }

      if (Connection != null)
      {
        if (Connection.State != ConnectionState.Closed)
        {
          Connection.Close();
        }

        Connection = null;
      }

      Command = null;
    }

    /// <summary>
    /// Releasing of unmanaged resources.
    /// </summary>
    /// <remarks>see also "Implementing a Dispose Method" at 
    /// https://msdn.microsoft.com/en-us/library/fs2xkftw(v=vs.110).aspx
    /// </remarks>
    public override void Dispose(bool disposing)
    {
      Connection?.Close();
      Connection?.Dispose();

      Connection = null;
    }

    /// <summary>
    /// Executes a SQL statement.
    /// </summary>
    /// <param name="sql">The SQL command text.</param>
    /// <exception cref="ApplicationException"></exception>
    public override void ExecSql(string sql)
    {
      if (Command == null)
        throw new ApplicationException(ResNoConnection);

      Command.CommandText = sql;
      Command.CommandTimeout = Timeout;
      Command.Transaction = Transaction;

      Command.ExecuteNonQuery();
    }

    /// <summary>
    /// Separates the provider from the rest of the connection string.
    /// </summary>
    /// <param name="connectionString"></param>
    /// <returns>An array with two entries, the first contains the provider 
    /// identifier and the second the connection string without provider 
    /// specification.</returns>
    private static string[] ExtractProvider(string connectionString)
    {
      var parameters = new List<string>();
      var result = new string[2];

      result[0] = "System.Data.SqlClient";

      foreach (string item in connectionString?.Split(';'))
      {
        var param = item.Split('=');

        if (param[0].ToLower() == "provider")
        {
          result[0] = param[1].ToLower() switch
          {
            // sqloledb.1 = Microsoft OLE DB - Treiber für SQL Server, for backwards compatibility
            "sqlclient" or "system.data.sqlclient" or "sqloledb.1" => "System.Data.SqlClient",
            // msdasql.1 = Microsoft OLE DB provider for ODBC, for backwards compatibility
            "odbc" or "system.data.odbc" or "msdasql.1" => "System.Data.Odbc",
            _ => param[1],
          };
        }
        else
          parameters.Add(item);
      }

      result[1] = string.Join(";", parameters);

      return result;
    }

    /// <summary>
    /// Return the compatibilty level for a MS SQL Server.
    /// </summary>
    /// <returns>The compatibilty level of the current database.</returns>
    /// <exception cref="ApplicationException"></exception>
    public override int GetCompatibilityLevel()
    {
      try
      {
        if (Command == null)
          throw new ApplicationException(ResNoConnection);

        Command.CommandText = "SELECT CONVERT(INT, [compatibility_level]) "
          + "FROM sys.databases WHERE name = DB_NAME()";
        Command.CommandTimeout = Timeout;
        Command.Transaction = Transaction;

        return (int)Command.ExecuteScalar();
      }
      catch (Exception exception)
      {
        throw new ApplicationException(ResErrCompLevel, exception);
      }
    }

    /// <summary>
    /// Returns the current value in seconds of the timeout for a SQL command. A value of 0 disables 
    /// the timeout. A value of 0 means that the timeout is disabled.
    /// </summary>
    /// <param name="timeout">The new timeout value in seconds.</param>
    public override int GetTimeout()
    {
      return Timeout;
    }

    /// <summary>
    /// Specifies whether a transaction is active.
    /// </summary>
    /// <returns><see langword="true"/> if a transaction active, otherwise 
    /// <see langword="false"/>.</returns>
    public override bool HasTransaction()
    {
      return Transaction != null;
    }

    /// <summary>
    /// Specifies whether a connection is open.
    /// </summary>
    /// <returns><see langword="true"/> if the connection is open, otherwise 
    /// <see langword="false"/>.</returns>
    public override bool IsOpen()
    {
      return Connection != null
          && Connection.State == ConnectionState.Open;
    }

    /// <summary>
    /// Executes a rollback and finished the running transaction.
    /// </summary>
    /// <exception cref="ApplicationException"></exception>
    /// <remarks>Transaction.Commit() and Transaction.Rollback() used the 
    /// connection timeout (default is 15 seconds). In large scripts with 
    /// many changes, this has resulted in a timeout error during commit.
    /// </remarks>
    public override void Rollback()
    {
      if (Transaction == null)
        throw new ApplicationException(ResNoTransaction);

      if (Connection is SqlConnection)
      {
        Command.CommandText = "ROLLBACK";
        Command.CommandTimeout = Timeout;
        Command.Transaction = Transaction;

        Command.CommandText = $"IF @@TRANCOUNT > 0 ROLLBACK ELSE THROW 50000, N'{ResServerSideRollback}', 1";

        Command.ExecuteNonQuery();
      }
      else 
        Transaction.Rollback();

      Transaction = null;
    }

    /// <summary>
    /// Sets the timeout in seconds for a SQL command. A value of 0 disables 
    /// the timeout.
    /// </summary>
    /// <param name="timeout">The new timeout value in seconds.</param>
    public override void SetTimeout(int timeout)
    {
      Timeout = Math.Max(timeout, 0);
    }
  }

}
