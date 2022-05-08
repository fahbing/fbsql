using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;


/// 2017-05-15 - created
/// 2021-12-12 - porting to .NET Standard 2.0

namespace Fahbing.Sql
{
  /// <summary>
  /// 
  /// </summary>
  /// <param name="value"></param>
  /// <returns></returns>
  public delegate string DecryptFunc(string value);

  /// <summary>
  /// Defines the various types of events for the <see cref="ScriptPlayer"/>.
  /// </summary>
  /// <param name="type">The type of the event, see <see cref="EventType"/>.
  /// </param>
  /// <param name="text">A message or command text.</param>
  /// <param name="value">A numeric (<see langword="long" />) parameter 
  /// value. Used in <see cref="EventType.cmdStart" />, <see 
  /// cref="EventType.cmdEnd" />,<see cref="EventType.scriptStart" /> and 
  /// <see cref="EventType.scriptCmd" />.</param>
  public delegate void EventAction(EventType type, string text, long value);

  /// <summary></summary>
  public enum EventType
  {
    /// <summary>A SQL command was executed.</summary>
    cmdEnd,
    /// <summary>A SQL command should be executed.</summary>
    cmdStart,
    /// <summary>A comment should be output.</summary>
    comment,
    /// <summary>The database connection has been opened.</summary>
    connected,
    /// <summary>The database connection has been closed.</summary>
    disconnected,
    /// <summary>An exception has been thrown.</summary>
    exception,
    /// <summary>A message from server should be output.</summary>
    message,
    /// <summary>A new script batch is running.</summary>
    newBatch,
    /// <summary>A new script step is running.</summary>
    newStep,
    /// <summary>A script command should be executed</summary>
    scriptCmd,
    /// <summary>The script was completly executed.</summary>
    scriptEnd,
    /// <summary>The script execution has been started.</summary>
    scriptStart,
    /// <summary>The transaction has been commited.</summary>
    tranCommited,
    /// <summary>The transaction has been rolled back.</summary>
    tranRollbacked,
    /// <summary>A new transaction has been launched.</summary>
    tranStarted,
    /// <summary>Transactions have been disabled.</summary>
    tranStopped,
    /// <summary>A warning should be output.</summary>
    warning,
  }


  /// <summary>
  /// A player to execute FBSQL scripts (Xml Sql Scripts: xss - files).
  /// </summary>
  public class ScriptPlayer
  {
    /// <summary>
    /// 
    /// </summary>
    internal class Placeholder
    {
      /// <summary>
      /// 
      /// </summary>
      public bool IsEncrypted { get; set; }

      /// <summary>
      /// 
      /// </summary>
      public string Value { get; set; }
    }

    private readonly DecryptFunc DecryptString;
    private readonly EventAction Action;
    private readonly SqlScriptConnection Connection;
    private string MonitorCommand;
    private string MonitorConnectionString;
    private readonly Dictionary<string, Placeholder> Placeholders;
    private readonly SqlTree ScriptCmds;
    private int TranCmdCount;
    private bool TranDisabled;

    /// <summary>The current exit code for the script.</summary>
    public int ExitCode { get; private set; }
    /// <summary>Interval in milliseconds for monitoring.</summary>
    public int MonInterval { get; set; } = 0;


    /// <summary>
    /// Creates a new instance of the <see cref="ScriptPlayer"/> class.
    /// </summary>
    public ScriptPlayer(SqlScriptConnection connection,
                        EventAction eventAction,
                        DecryptFunc decryptFunc)
    {
      Connection = connection;
      Action = eventAction;
      DecryptString = decryptFunc;
      MonitorCommand = "IF (OBJECT_ID('tempDB..##xssMonitor') IS NOT NULL) EXEC ##xssMonitor";
      Placeholders = new Dictionary<string, Placeholder>();
      ScriptCmds = new SqlTree();
      TranCmdCount = 0;
    }

    /// <summary>
    /// Commits the current transaction and starts a new.
    /// </summary>
    private void Commit()
    {
      if (TranDisabled)
        throw new Exception("Transactions are disabled.");

      if (!Connection.HasTransaction())
        throw new Exception("No transaction is active.");

      Connection.Commit();
      Action?.Invoke(EventType.tranCommited, "", 0);

      TranCmdCount = 0;

      Connection.BeginTransaction();
      Action?.Invoke(EventType.tranStarted, "", 0);
    }

    /// <summary>
    /// Opens a connection to the database.
    /// </summary>
    /// <param name="connectionString">A connection string to establish the 
    /// connection.</param>
    private void Connect(string connectionString)
    {
      Disconnect();

      MonitorConnectionString = connectionString;
      Connection.OnMessage = (string message) =>
        {
          if (message.StartsWith("warn:"))
            Action?.Invoke(EventType.warning
                         , message.Substring(5).TrimStart(), 0);
          else
            Action?.Invoke(EventType.message, message, 0);
        };

      Connection.Connect(connectionString);

      Action?.Invoke(EventType.connected, connectionString, 0);

      if (MonInterval > 0)
        ExecMonitor();

      TranCmdCount = 0;
      
      Connection.BeginTransaction();
      Action?.Invoke(EventType.tranStarted, "", 0);
    }

    /// <summary>
    /// Closes the connection to the database.
    /// </summary>
    private void Disconnect()
    {
      if (Connection.HasTransaction())
      {
        Connection.Rollback();

        if (TranCmdCount > 0)
          Action?.Invoke(EventType.tranRollbacked, "", 0);

        TranCmdCount = 0;
      }

      if (Connection.IsOpen())
      {
        if (MonInterval > 0)
          ExecMonitor();

        Connection.Disconnect();
        Action?.Invoke(EventType.disconnected, "", 0);
      }
    }

    /// <summary>
    /// Executes the loaded script.
    /// </summary>
    public void Exec()
    {
      ExitCode = -1;
      int count = 0;
      bool monitoring = MonInterval > 0;
      Stopwatch cmdWatch = new();
      Stopwatch scriptWatch = new();

      ScriptCmds.ForEachExecutableCommand((item, command, index) =>
      {
        if (item.ItemType == SqlTreeItemType.step)
          count++;
      });

      Action?.Invoke(EventType.scriptStart, "", count);
      scriptWatch.Start();

      if (monitoring)
      {
        Task.Run(() =>
        {
          while (monitoring)
          {
            ExecMonitor();
            Thread.Sleep(MonInterval);
          }
        });
      }

      try
      {
        try
        {
          ScriptCmds.ForEachExecutableCommand((item, command, index) =>
          {
            switch (item.ItemType)
            {
              case SqlTreeItemType.batch:
                Action?.Invoke(EventType.newBatch, item.GetPath(), 0);
                break;
              case SqlTreeItemType.step:
                if (index == 0)
                  Action?.Invoke(EventType.newStep, item.GetPath(), 0);

                cmdWatch.Restart();

                try
                {
                  ExecCommand(command, index);
                }
                finally
                {
                  cmdWatch.Stop();

                  Action?.Invoke(EventType.cmdEnd, cmdWatch.Elapsed.ToString()
                    + $", total: {scriptWatch.Elapsed.ToString()}"
                    + $", {DateTime.Now.ToString("o")}"
                    , cmdWatch.ElapsedMilliseconds);
                }
                break;
              default:
                break;
            }
          });

          if (TranCmdCount > 0)
            Commit();

          ExitCode = 0;
        }
        catch (Exception exception)
        {
          Action?.Invoke(EventType.exception, exception.Message, 0);
        }
      }
      finally
      {
        monitoring = false;

        Disconnect();
        scriptWatch.Stop();
        Action?.Invoke(EventType.scriptEnd, scriptWatch.Elapsed.ToString(), 1);
      }
    }

    /// <summary>
    /// Executes a SQL or script command.
    /// </summary>
    /// <param name="command">A <see cref="SqlScriptCommand"/> instance that 
    /// defines the command.</param>
    /// <param name="cmdIndex">The index of a command in a step.</param>
    private void ExecCommand(SqlScriptCommand command,
                             int cmdIndex = 0)
    {
      string sql = ReplacePlaceholder(command.Sql, true);
      string text = ReplacePlaceholder(command.Sql);

      Action?.Invoke(EventType.cmdStart, text, cmdIndex);

      switch (command.CmdType)
      {
        case SqlScriptCmdType.comment:
          Action?.Invoke(EventType.comment, text, 0);
          break;
        case SqlScriptCmdType.commit:
          Action?.Invoke(EventType.scriptCmd, "COMMIT", 0);
          Commit();
          break;
        case SqlScriptCmdType.connect:
          sql = ReplacePlaceholder(command.CmdParams[0]?.ToString(), true);
          text = ReplacePlaceholder(command.CmdParams[0]?.ToString());

          Action?.Invoke(EventType.scriptCmd
                       , $"CONNECT '{text.Replace("'", "''")}'", 0);
          Connect(sql);
          break;
        case SqlScriptCmdType.disconnect:
          Action?.Invoke(EventType.scriptCmd, "DISCONNECT", 0);
          Disconnect();
          break;
        case SqlScriptCmdType.rollback:
          Action?.Invoke(EventType.scriptCmd, "ROLLBACK", 0);
          Rollback();
          break;
        case SqlScriptCmdType.setPlaceholder:
          SetPlaceholder(command.CmdParams[0]?.ToString()
            , command.CmdParams[1]?.ToString());
          break;
        case SqlScriptCmdType.sql:
          if (!Connection.HasTransaction())
            Action?.Invoke(EventType.warning
                         , "No transaction is currently active.", 0);

          Connection.ExecSql(sql);

          if (Connection.HasTransaction())
            ++TranCmdCount;

          break;
        case SqlScriptCmdType.setExitCode:
          var exitCode = (int)command.CmdParams[0];

          Action?.Invoke(EventType.scriptCmd, $"SET EXITCODE {exitCode}"
                       , exitCode);

          ExitCode = exitCode;

          Action?.Invoke(EventType.comment
            , $"exit code is changed to {ExitCode}", 0);
          break;
        case SqlScriptCmdType.setTimeout:
          var timeout = (int)command.CmdParams[0];
          Action?.Invoke(EventType.scriptCmd, $"SET TIMEOUT {timeout}", 0);

          Connection.SetTimeout(timeout);

          Action?.Invoke(EventType.comment
            , $"command timeout changed to {Connection.GetTimeout()}", 0);
          break;
        case SqlScriptCmdType.startTransaction:
          StartTransaction();
          break;
        case SqlScriptCmdType.stopTransaction:
          StopTransaction();
          break;
      }
    }

    /// <summary>
    /// Executes the SQL command for the monitoring.
    /// </summary>
    private void ExecMonitor()
    {
      if (string.IsNullOrEmpty(MonitorConnectionString)
        || Connection == null || !Connection.IsOpen())
        return;
      
      try
      {
        using SqlScriptConnection monitorCon = Connection.Create();
        monitorCon.OnMessage = (message) =>
        {
          Action?.Invoke(EventType.message, message, 0);
        };

        monitorCon.SetTimeout(1);
        monitorCon.Connect(MonitorConnectionString);
        monitorCon.ExecSql(MonitorCommand);
        monitorCon.Disconnect();
      }
      catch
      {
        // nothing
      }
    }

    /// <summary>
    /// Loads the configuration from a JSON file.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="jsonPath">A string that contains a JSON path expression 
    /// to select a section.</param>
    public void LoadConfigFromJsonFile(string fileName,
                                       string jsonPath = "$")
    {
      if (!string.IsNullOrEmpty(fileName))
      {
        var config = JObject.Parse(File.ReadAllText(fileName))
                            .SelectToken(jsonPath);

        if (config != null
          && config["placeholders"] is JObject placeholder)
        {
          foreach (JProperty item in placeholder.Properties())
          {
            if (item.Value is JObject && Convert.ToBoolean(item.Value["encrypted"]))
              SetPlaceholder(item.Name
                           , DecryptString(item.Value["value"]?.ToString())
                           , true);
            else
              SetPlaceholder(item.Name, item.Value?.ToString());
          }
        }
      }
    }

    /// <summary>
    /// Load the script content from a directory.
    /// </summary>
    /// <param name="path">A file system path to the directory.</param>
    /// <param name="action">A <see cref="LoadAction"/> delegate function, 
    /// e.B. for progress indicators.</param>
    public void LoadFromDirectory(string path
                                , LoadAction action = null)
    {
      ScriptCmds.LoadFromDirectory(path, action);
    }

    /// <summary>
    /// Load the script content from an XML file.
    /// </summary>
    /// <param name="fileName">The path of the file to load.</param>
    public void LoadFromXmlFile(string fileName)
    {
      ScriptCmds.LoadFromXmlFile(fileName);
    }

    /// <summary>
    /// Replaces all placeholder equivalents in a string with the values 
    /// assigned to the placeholders.
    /// </summary>
    /// <param name="value">The string in which replacements are to be made.
    /// </param>
    /// <param name="decrypted"></param>
    /// <returns>The string with the exchanged placeholders.</returns>
    private string ReplacePlaceholder(string value, bool decrypted = false)
    {
      var result = value;

      foreach (var item in Placeholders)
      {
        result = result.Replace(item.Key, item.Value.IsEncrypted && !decrypted
                              ? "*****" : item.Value.Value);
      }

      return result;
    }

    /// <summary>
    /// Executes a rollback and finished the running transaction.
    /// </summary>
    private void Rollback()
    {
      if (Connection.HasTransaction())
      {
        Connection.Rollback();
        Action?.Invoke(EventType.tranRollbacked, "", 0);

        TranCmdCount = 0;
        
        Connection.BeginTransaction();
        Action?.Invoke(EventType.tranStarted, "", 0);
      }
      else
        throw new Exception("No transaction is active.");
    }

    /// <summary>
    /// Sets a new value for a placeholder.
    /// </summary>
    /// <param name="key">The placeholder name.</param>
    /// <param name="value">The new placeholder value.</param>
    private void SetPlaceholder(string key
                              , string value
                              , bool IsEncrypted = false)
    {
      Placeholders[key] = new Placeholder()
      {
        IsEncrypted = IsEncrypted,
        Value = value
      };

      if (IsEncrypted)
        value = "*****";

      key = key.Replace("'", "''");
      value = value.Replace("'", "''");

      Action?.Invoke(EventType.scriptCmd
        , $"SET PLACEHOLDER '{key}' '{value}'; ", 0);
    }

    /// <summary>
    /// Starts a new database transaction.
    /// </summary>
    private void StartTransaction()
    {
      Action?.Invoke(EventType.scriptCmd, "STARTRANSACTION", 0);

      TranCmdCount = 0;
      TranDisabled = false;
      
      Connection.BeginTransaction();
      Action?.Invoke(EventType.tranStarted, "", 0);
    }

    /// <summary>
    /// Disables transaction mode. When a transaction is active, a rollback is 
    /// performed. A warning is issued for the following SQL commands.
    /// </summary>
    private void StopTransaction()
    {
      Action?.Invoke(EventType.scriptCmd, "STOPTRANSACTION", 0);

      if (Connection.HasTransaction())
      {
        Connection.Rollback();

        TranCmdCount = 0;
        TranDisabled = true;

        Action?.Invoke(EventType.tranRollbacked, "", 0);
      }

      Action?.Invoke(EventType.tranStopped, "", 0);
    }
  }

}
