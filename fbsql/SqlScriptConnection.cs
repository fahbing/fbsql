using System;


/// 2021-12-12 - created

namespace Fahbing.Sql
{
  /// <summary>
  /// A delegate function to receive messages from the database server.
  /// </summary>
  public delegate void SqlScriptConnectionMessage(string message);


  /// <summary>
  /// An abstract connection class for the SqlScriptPlayer.
  /// </summary>
  public abstract class SqlScriptConnection : IDisposable
  {
    /// <summary>
    /// Gets or sets a delegate function to receive messages from the database 
    /// server.
    /// </summary>
    public SqlScriptConnectionMessage OnMessage { get; set; }


    /// <summary>
    /// Finalizer for <see cref="SqlScriptConnection"/>.
    /// </summary>
    ~SqlScriptConnection()
    {
      Dispose(false);
    }

    /// <summary>
    /// Creates a new instance of it descendant.
    /// </summary>
    /// <returns>The new instance of the descendant.</returns>
    public SqlScriptConnection Create() 
    {
      return (SqlScriptConnection)Activator.CreateInstance(this.GetType());
    }

    /// <summary>
    /// Starts a new transaction.
    /// </summary>
    public abstract void BeginTransaction();

    /// <summary>
    /// Executes a commit and finished the running transaction.
    /// </summary>
    public abstract void Commit();

    /// <summary>
    /// Opens the connection to the database.
    /// </summary>
    public abstract void Connect(string connectionString);

    /// <summary>
    /// Closes the connection to the database.
    /// </summary>
    public abstract void Disconnect();

    /// <summary>
    /// Releasing of unmanaged resources.
    /// </summary>
    /// <remarks>see also "Implementing a Dispose Method" at 
    /// https://msdn.microsoft.com/en-us/library/fs2xkftw(v=vs.110).aspx
    /// </remarks>
    public virtual void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releasing of unmanaged resources.
    /// </summary>
    /// <param name="disposing">Dispose(bool disposing) executes in two 
    /// distinct scenarios. If disposing equals <see langword="true"/>, the 
    /// method has been called directly or indirectly by a user's code. 
    /// Managed and unmanaged resources can be disposed. If disposing equals 
    /// <see langword="false"/>, the method has been called by the runtime 
    /// from inside the finalizer and you should not reference other objects. 
    /// Only unmanaged resources can be disposed.</summary>
    /// </param>
    public abstract void Dispose(bool disposing);

    /// <summary>
    /// Executes a SQL statement.
    /// </summary>
    /// <param name="sql">The SQL command text.</param>
    /// <exception cref="ApplicationException"></exception>
    public abstract void ExecSql(string sql);

    /// <summary>
    /// Returns the current value in seconds of the timeout for a SQL command. 
    /// A value of 0 means that the timeout is disabled.
    /// </summary>
    /// <param name="timeout">The new timeout value in seconds.</param>
    public abstract int GetTimeout();

    /// <summary>
    /// Specifies whether a transaction is active.
    /// </summary>
    /// <returns><see langword="true"/> if a transaction active, otherwise 
    /// <see langword="false"/>.</returns>
    public abstract bool HasTransaction();

    /// <summary>
    /// Specifies whether the connection is open.
    /// </summary>
    /// <returns><see langword="true"/> if the connection is open, otherwise 
    /// <see langword="false"/>.</returns>
    public abstract bool IsOpen();

    /// <summary>
    /// Executes a rollback and finished the running transaction.
    /// </summary>
    /// <exception cref="ApplicationException"></exception>
    public abstract void Rollback();

    /// <summary>
    /// Sets the timeout in seconds for a SQL command. A value of 0 disables 
    /// the timeout.
    /// </summary>
    /// <param name="timeout">The new timeout value in seconds.</param>
    public abstract void SetTimeout(int timeout);
  }

}
