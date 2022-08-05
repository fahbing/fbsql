using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Data.SqlClient;


/// 2021-12-12 - created

namespace Fahbing.Sql
{
  /// <summary>
  /// A connection object for the <see cref="SqlScriptPlayer"/> that implements 
  /// the system.data.sqlclient and system.data.oledb connection provider.
  /// </summary>
  internal class PlayerConnection : SqlScriptConnection
  {
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
        throw new ApplicationException("No transaction is active.");

      Command.CommandText = "COMMIT";
      Command.CommandTimeout = Timeout;
      Command.Transaction = Transaction;

      Command.ExecuteNonQuery();
      //Transaction.Commit();

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

      Connection = (parameters[0]?.ToLower()) switch
      {
        "sqloledb.1" or "system.data.sqlclient" => new SqlConnection(),
        "msdasql.1" or "system.data.oledb" => new OleDbConnection(),
        _ => throw new ApplicationException(
          $"Unknown or not supported provider name {parameters[0]}"),
      };

      if (Connection is SqlConnection connection)
      {
        SqlConnectionStringBuilder builder = new(parameters[1]);
        builder.ApplicationName = "FBSQL Command Line Script Player";
        Connection.ConnectionString = builder.ConnectionString;

        connection.InfoMessage += (object sender
        , SqlInfoMessageEventArgs e) =>
        {
          OnMessage?.Invoke(e.Message);
        };
      }
      else
        Connection.ConnectionString = parameters[1];

      Connection.Open();

      Command = Connection.CreateCommand();
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
        throw new ApplicationException("No connection is active.");

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
            "sqloledb.1" or "system.data.sqlclient" => "System.Data.SqlClient",
            "msdasql.1" or "system.data.oledb" => "System.Data.OleDb",
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
        throw new ApplicationException("No transaction is active.");

      Command.CommandText = "ROLLBACK";
      Command.CommandTimeout = Timeout;
      Command.Transaction = Transaction;

      Command.ExecuteNonQuery();
      //Transaction.Rollback();

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
