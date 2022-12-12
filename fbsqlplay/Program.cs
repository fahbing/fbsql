using Fahbing.Sql;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;


/// 2017-05-15 - created
/// 2022-05-75 - support of encrypted strings for placeholders

namespace Fahbing
{
  /// <summary>
  /// FBSQL Command Line Script Player
  /// </summary>
  class Program
  {
    /// <summary>
    /// A struct type for the command line arguments evaluation.
    /// </summary>
    private struct CommandLineArgs
    {
      /// <summary>Play sound after script run.</summary>
      public bool Alarm;
      /// <summary>Build a script file from a folder script.</summary>
      public bool Build;
      /// <summary>Name incl. path of the JSON formated configuration file.
      /// </summary>
      public string BuildFile;
      /// <summary>The configuration file path. A configuration file is a JSON 
      /// file with a "fbSqlScript" property that contains the configuration 
      /// object for this program.</summary>
      public string ConfigFile;
      /// <summary>A Json path to the config section in configFile. The 
      /// default is "$".</summary>
      public string ConfigJPath;
      /// <summary>Delete log file after a successful script run.</summary>
      public bool DeleteLog;
      /// <summary>Encrypt a string.</summary>
      public bool EncryptString;
      /// <summary>The log file name.</summary>
      public string LogFile;
      /// <summary>The monitor interval in milliseconds.</summary>
      public int MonInterval;
      /// <summary>The name of a placeholder.</summary>
      public string placeholderName;
      /// <summary>Reduce the text output in the terminal. This does not apply 
      /// to the output in the log file.</summary>
      public bool ReducedOutput;
      /// <summary>Show help screen.</summary>
      public bool ShowHelp;
      /// <summary>The Script file or folder.</summary>
      public string ScriptFile;
      /// <summary>Run script in silent mode.</summary>
      public bool SilentMode;
    }


    /// <summary>Standard background/foreground color.</summary>
    static private readonly ConsoleColor[] StandardColor
      = { Console.BackgroundColor, Console.ForegroundColor };

    /// <summary>Color configuration for action messages.</summary>
    private static readonly ConsoleColor[] ActionColor
      = { ConsoleColor.Black, ConsoleColor.White };

    /// <summary>Color configuration for argumants.</summary>
    private static readonly ConsoleColor[] ArgumentColor
      = { ConsoleColor.Black, ConsoleColor.Cyan };

    /// <summary></summary>
    private static DebugInfo CmdDebugInfo;

    /// <summary>The command line argumants in a <see cref="CommandLineArgs"/>
    /// variable.</summary>
    private static CommandLineArgs Arguments;

    /// <summary>Color configuration for commands.</summary>
    private static readonly ConsoleColor[] CommandColor
      = { ConsoleColor.Black, ConsoleColor.Yellow };

    /// <summary>Color configuration for comments.</summary>
    private static readonly ConsoleColor[] CommentColor
      = { ConsoleColor.Black, ConsoleColor.Gray };

    /// <summary>Color configuration for error messages.</summary>
    private static readonly ConsoleColor[] ErrorColor
      = { ConsoleColor.DarkRed, ConsoleColor.Gray };

    /// <summary>Stores the current exit code for this program.</summary>
    private static int ExitCode = 1;

    /// <summary>A dedicated object for setting up thread locks.</summary>
    static private readonly object LockObject = new();

    /// <summary>Color configuration for messages.</summary>
    private static readonly ConsoleColor[] MessageColor
      = { ConsoleColor.Black, ConsoleColor.DarkGreen };

    /// <summary>Color configuration for step types.</summary>
    private static readonly ConsoleColor[] StepTypeColor
      = { ConsoleColor.DarkBlue, ConsoleColor.White };

    /// <summary>Color configuration for tree paths.</summary>
    private static readonly ConsoleColor[] TreePathColor
      = { ConsoleColor.Black, ConsoleColor.Green };

    /// <summary>Color configuration for warnings.</summary>
    private static readonly ConsoleColor[] WarningColor
      = { ConsoleColor.Black, ConsoleColor.DarkYellow };


    /// <summary>
    /// Creates an XML file version of a folder script.
    /// </summary>
    /// <param name="sourceFolder">The path of a folder script.</param>
    /// <param name="destFile">The path of the destination script file.</param>
    /// <param name="logFileName">The log file name.</param>
    private static void BuildFile(string sourceFolder,
                                  string destFile,
                                  string logFileName)
    {
      try
      {
        Console.WriteLine($"\nload script from {sourceFolder}.");
        Log(logFileName, $"\nload script from {sourceFolder}.");

        SqlTree script = new();

        script.LoadFromDirectory(sourceFolder, (type, path) =>
        {
          if (type == SqlTreeItemType.batch)
          {
            Console.WriteLine($"load batch: {path}");
            Log(logFileName, $"load batch: {path}");
          }
          else
          {
            Console.WriteLine($"load step:  {path}");
            Log(logFileName, $"load step:  {path}");
          }
        });
        
        Console.WriteLine($"\nwrite script to  {destFile}.");
        Log(logFileName, $"\nwrite script to  {destFile}.");

        script.SaveToXmlFile(destFile);

        Console.WriteLine("\nfinish");
        Log(logFileName, "\nfinish");

        ExitCode = 0;
      }
      catch (Exception exception)
      {
        WriteException(exception, logFileName);
      }
    }

    /// <summary>
    /// Decrypts a string.
    /// </summary>
    /// <param name="value">The string to decrypt.</param>
    /// <returns>The decrypted string.</returns>
    /// https://docs.microsoft.com/de-de/dotnet/api/system.security.cryptography.protecteddata.unprotect?view=dotnet-plat-ext-6.0
    private static string DecryptString(string value)
    {
      byte[] encryptedData = Convert.FromBase64String(value);
      byte[] userData = ProtectedData.Unprotect(encryptedData, null
                      , DataProtectionScope.CurrentUser);

      return Encoding.Unicode.GetString(userData);
    }

    /// <summary>
    /// Takes repeated inputs of a string and stores it encrypted as 
    /// placeholder value in the specified configuration file.
    /// </summary>
    /// <param name="configFile">The path of the configuration file.</param>
    /// <param name="configJPath">A Json path to the config section in the 
    /// configuration file.</param>
    /// <param name="placeholderName">The placeholder name.</param>
    private static void EncryptEnteredString(string configFile,
                                             string configJPath,
                                             string placeholderName)
    {
      while (true)
      {
        Console.WriteLine();
        Console.Write("enter the string to encrypt: ");
        string value = ReadString(true);

        if (string.IsNullOrEmpty(value))
        {
          WriteError("The string to be encrypted must not be empty!");
          continue;
        }

        value = EncryptString(value);

        Console.WriteLine();
        Console.Write("re-enter the string to encrypt: ");

        string confirmValue = ReadString(true);

        if (DecryptString(value) == confirmValue)
        {
          if (!SavePlaceholderValue(configFile, configJPath, placeholderName
                                  , value, true))
          {
            Console.WriteLine(value);
            Console.ReadLine();
          }
          break;
        }
        else
          WriteError("The two strings entered are not identical!");
      }
    }

    /// <summary>
    /// Encrypts a string.
    /// </summary>
    /// <param name="value">The string to encrypt.</param>
    /// <returns>The encrypted string.</returns>
    /// https://docs.microsoft.com/de-de/dotnet/api/system.security.cryptography.protecteddata.protect?view=dotnet-plat-ext-6.0
    private static string EncryptString(string value)
    {
      byte[] userData = Encoding.Unicode.GetBytes(value);
      byte[] encryptedData = ProtectedData.Protect(userData, null
                       , DataProtectionScope.CurrentUser);

      return Convert.ToBase64String(encryptedData);
    }

    /// <summary>
    /// Evaluates the command line arguments and returns them in an <see 
    /// cref="CommandLineArgs"/> struct.
    /// </summary>
    /// <param name="args">The command line arguments from the <see 
    /// cref="Main"/> function.</param>
    /// <returns>A filled <see cref="CommandLineArgs"/> object.</returns>
    private static CommandLineArgs GetArguments(string[] args)
    {
      CommandLineArgs result;

      result.Alarm = false;
      result.Build = false;
      result.BuildFile = "";
      result.ConfigFile = "";
      result.ConfigJPath = "$";
      result.DeleteLog = false;
      result.EncryptString = false;
      result.LogFile = "";
      result.MonInterval = 0;
      result.placeholderName = "";
      result.ReducedOutput = false;
      result.ShowHelp = false;
      result.ScriptFile = "";
      result.SilentMode = false;

      for (int index = 0; index < args.Length; index++)
      {
        string argument = args[index];
        if (argument.StartsWith("/l:", StringComparison.OrdinalIgnoreCase)
         || argument.StartsWith("-l:", StringComparison.OrdinalIgnoreCase))
        {
          result.LogFile = argument.Substring(3);

          if (Directory.Exists(result.LogFile))
          {
            result.LogFile = Path.Combine(result.LogFile, $"SQL_"
                           + Environment.MachineName + "_"
                           + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff")
                           + ".log");
          }
        }
        else
        if (argument.Equals("/l", StringComparison.OrdinalIgnoreCase)
         || argument.Equals("-l", StringComparison.OrdinalIgnoreCase))
          result.LogFile = System.IO.Path.GetTempPath()
                         + $"SQL_"
                         + Environment.MachineName + "_"
                         + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff")
                         + ".log";
        else
        if (argument.Equals("/r", StringComparison.OrdinalIgnoreCase)
         || argument.Equals("-r", StringComparison.OrdinalIgnoreCase))
          result.ReducedOutput = true;
        else
        if (argument.Equals("/s", StringComparison.OrdinalIgnoreCase)
         || argument.Equals("-s", StringComparison.OrdinalIgnoreCase))
          result.SilentMode = true;
        else
        if (argument.StartsWith("/c:", StringComparison.OrdinalIgnoreCase)
         || argument.StartsWith("-c:", StringComparison.OrdinalIgnoreCase))
          result.ConfigFile = argument.Substring(3);
        else
        if (argument.StartsWith("/p:", StringComparison.OrdinalIgnoreCase)
         || argument.StartsWith("-p:", StringComparison.OrdinalIgnoreCase))
          result.ConfigJPath = argument.Substring(3);
        else
        if (argument.StartsWith("/m:", StringComparison.OrdinalIgnoreCase)
         || argument.StartsWith("-m:", StringComparison.OrdinalIgnoreCase))
        {
          if (!Int32.TryParse(argument.Substring(3), out result.MonInterval))
            result.MonInterval = -1;
        }
        else
        if (argument.Equals("/a", StringComparison.OrdinalIgnoreCase)
         || argument.Equals("-a", StringComparison.OrdinalIgnoreCase))
          result.Alarm = true;
        else
        if (argument.Equals("/b", StringComparison.OrdinalIgnoreCase)
         || argument.Equals("-b", StringComparison.OrdinalIgnoreCase))
          result.Build = true;
        else
        if (argument.StartsWith("/b:", StringComparison.OrdinalIgnoreCase)
         || argument.StartsWith("-b:", StringComparison.OrdinalIgnoreCase))
        {
          result.Build = true;
          result.BuildFile = argument.Substring(3);
        }
        else
        if (argument.Equals("/d", StringComparison.OrdinalIgnoreCase)
         || argument.Equals("-d", StringComparison.OrdinalIgnoreCase))
          result.DeleteLog = true;
        else
        if (argument.Equals("/e", StringComparison.OrdinalIgnoreCase)
         || argument.Equals("-e", StringComparison.OrdinalIgnoreCase))
          result.EncryptString = true;
        else
        if (argument.StartsWith("/k:", StringComparison.OrdinalIgnoreCase)
         || argument.StartsWith("-k:", StringComparison.OrdinalIgnoreCase))
          result.placeholderName = argument.Substring(3);
        else
        if (argument.Equals("?:", StringComparison.OrdinalIgnoreCase)
         || argument.Equals("/?", StringComparison.OrdinalIgnoreCase)
         || argument.Equals("-?", StringComparison.OrdinalIgnoreCase)
         || argument.Equals("/h", StringComparison.OrdinalIgnoreCase)
         || argument.Equals("-h", StringComparison.OrdinalIgnoreCase)
         || argument.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
          result.ShowHelp = true;
        }
        else
          result.ScriptFile = argument;
      }

      if (result.Build && result.BuildFile == "")
        result.BuildFile = Path.Combine(result.ScriptFile, "script.xss");

      return result;
    }

    /// <summary>
    /// Gets the current version of the assembly.
    /// </summary>
    /// <returns>The assembly version as string</returns>
    private static string GetAssemblyVersion()
    {
      Assembly assembly = Assembly.GetAssembly(typeof(Program));

      return assembly?.GetName().Version?.ToString() ?? "";
    }

    /// <summary>
    /// Handles the events from the <see cref="ScriptPlayer"/> instance while 
    /// the script is running.
    /// </summary>
    /// <param name="type">The type of the event, see <see cref="EventType"/>.
    /// </param>
    /// <param name="text">A message or command text.</param>
    /// <param name="value">A numeric (<see langword="long" />) parameter 
    /// value. Used in <see cref="EventType.cmdStart" />, <see 
    /// cref="EventType.cmdEnd" />,<see cref="EventType.scriptStart" /> and 
    /// <see cref="EventType.scriptCmd" />.</param>
    /// <param name="sourcePath"></param>
    private static void HandlePlayerAction(EventType type,
                                           string text,
                                           long value,
                                           DebugInfo debugInfo = null)
    {
      switch (type)
      {
        case EventType.cmdEnd:
          lock (LockObject)
          {
            WriteComment($"elapsed time for command: {text}"
                       , Arguments.LogFile, Arguments.ReducedOutput);
            Log(Arguments.LogFile, "");

            if (!Arguments.ReducedOutput)
              Console.WriteLine();
          }
          break;
        case EventType.cmdStart:
          CmdDebugInfo = debugInfo;

          WriteTreePath("cmd", "#" + (++value).ToString().PadLeft(3, '0')
                      , Arguments.LogFile);
          WriteCommand(text, Arguments.LogFile, Arguments.ReducedOutput);

          break;
        case EventType.comment:
          WriteComment(text, Arguments.LogFile);
          break;
        case EventType.connected:
          WriteComment("server is connected", Arguments.LogFile);
          break;
        case EventType.disconnected:
          WriteComment("server is disconnected", Arguments.LogFile);
          break;
        case EventType.exception:
          WriteError(text, Arguments.LogFile);
         
          if (!string.IsNullOrEmpty(CmdDebugInfo?.SourcePath))
            WriteError($"on {new Uri(CmdDebugInfo.SourcePath).AbsoluteUri}"
              + (CmdDebugInfo.Line > 0 ? $" - {CmdDebugInfo.Line}:1" : "")
              , Arguments.LogFile);

          break;
        case EventType.message:
          WriteMessage(text, Arguments.LogFile, Arguments.ReducedOutput);

          break;
        case EventType.newBatch:
          WriteTreePath("batch", text, Arguments.LogFile);
          break;
        case EventType.newStep:
          WriteTreePath("step", text, Arguments.LogFile);
          break;
        case EventType.scriptCmd:
          if (text.StartsWith("SET EXITCODE "))
            ExitCode = (int)value;

          WriteScriptCommand(text, Arguments.LogFile);
          break;
        case EventType.scriptEnd:
          WriteScriptEnd(text, Arguments.LogFile);
          break;
        case EventType.scriptStart:
          WriteScriptStart(Arguments.LogFile);
          break;
        case EventType.tranCommited:
          WriteComment("transaction is commited", Arguments.LogFile);
          break;
        case EventType.tranRollbacked:
          WriteComment("transaction is rollbacked", Arguments.LogFile);
          break;
        case EventType.tranStarted:
          WriteComment("transaction is started", Arguments.LogFile);
          break;
        case EventType.warning:
          WriteWarning(text, Arguments.LogFile);
          break;
      }
    }

    /// <summary>
    /// Outputs the help screen.
    /// </summary>
    private static void Help()
    {
      Console.WriteLine();
      Console.WriteLine("   fbsqlplay scriptPath /c:cfgFile /p:cfgJsonPath /m:int32 /l[:dir|file]");
      Console.WriteLine("             /a /d /s");
      Console.WriteLine();
      Console.WriteLine("   /a                Play sound after script run.");
      Console.WriteLine("   /c:cfgFile        Name incl. path of the JSON formated configuration file.");
      Console.WriteLine("   /d                Delete log file after a successful script run.");
      Console.WriteLine("   /l[:dir|file]");
      Console.WriteLine("     /l                Output a log file into the temp folder.");
      Console.WriteLine("     /l:dir            Output a log file into the specified folder.");
      Console.WriteLine("     /l:file           Output a log file into the specified file.");
      Console.WriteLine("   /m:int32          The monitor interval in milliseconds.");
      Console.WriteLine("   /p:cfgJsonPath    A Json path to the config section in configFile.");
      Console.WriteLine("                     The default is \"$\".");
      Console.WriteLine("   /r                Reduce the text output in the terminal. This does not ");
      Console.WriteLine("                     apply to the output in the log file.");
      Console.WriteLine("   /s                The script runs in silent mode.");
      Console.WriteLine();
      Console.WriteLine("   fbsqlplay scriptFolder /b[:fileName] /l[:dir|file] /a");
      Console.WriteLine();
      Console.WriteLine("   /b[:fileName]     Build a script file from a folder script. The default file ");
      Console.WriteLine("                     name is \"script.xss\" in the script folder.");
      Console.WriteLine();
      Console.WriteLine("   fbsqlplay /e [/c:cfgFile [/p:cfgJsonPath] /k:placeholderName]");
      Console.WriteLine();
      Console.WriteLine("   /e                Encrypt a string.");
      Console.WriteLine("   /c:cfgFile        Name incl. path of the JSON formated configuration file.");
      Console.WriteLine("   /p:cfgJsonPath    A Json path to the config section in configFile.");
      Console.WriteLine("                     The default is \"$\".");
      Console.WriteLine("   /k:placeholder    The key name of the placeholder to which the encrypted ");
      Console.WriteLine("                     string is stored.");
      Console.WriteLine();
      Console.WriteLine(new string('-', 80));
    }

    /// <summary>
    /// Deletes the log file if it already exists.
    /// </summary>
    /// <param name="logFileName">The log file name.</param>
    private static void InitLogFile(string logFile)
    {
      if (File.Exists(logFile))
        File.Delete(logFile);
    }

    /// <summary>
    /// Loads a script from an XML file or a folder.
    /// </summary>
    /// <param name="scriptPlayer">A <see cref="ScriptPlayer"/> instance.
    /// </param>
    /// <param name="scriptFileName">The script file path.</param>
    /// <param name="logFileName">The log file path.</param>
    private static void LoadScriptFile(ScriptPlayer scriptPlayer,
                                       string scriptFileName,
                                       string logFileName)
    {
      WriteAction($"load script from {scriptFileName}.", logFileName);
      string errorPath = scriptFileName;

      try
      {
        FileAttributes fileAttr = File.GetAttributes(scriptFileName);

        if ((fileAttr & FileAttributes.Directory) == FileAttributes.Directory)
        {
          scriptPlayer.LoadFromDirectory(scriptFileName, (type, path) =>
          {
            errorPath = path;

            if (path.Length > 65)
              path = "..." + path.Substring(path.Length - 62, 62);

            path = path.PadRight(65);
            Console.CursorVisible = false;

            try
            {
              Console.SetCursorPosition(0, Console.CursorTop);

              if (type == SqlTreeItemType.batch)
                Console.Write($"   load batch: {path}");
              else
                Console.Write($"   load step:  {path}");
            }
            finally
            {
              Console.CursorVisible = true;
            }
          }, true);

          Console.SetCursorPosition(0, Console.CursorTop);
        }
        else
          scriptPlayer.LoadFromXmlFile(scriptFileName);

        errorPath = null;

        WriteAction($"finish".PadRight(80), logFileName);
      }
      catch (Exception exception)
      {
        if (!string.IsNullOrEmpty(errorPath))
          throw new ApplicationException($"on {new Uri(errorPath).AbsoluteUri}"
                                       , exception);
        else
          throw exception;
      }
    }

    /// <summary>
    /// Loads external configuration settings from a JSON file.
    /// </summary>
    /// <param name="scriptPlayer">A <see cref="ScriptPlayer"/> instance.
    /// </param>
    /// <param name="configFileName">The path of the JSON file to load the 
    /// configuration settings.</param>
    /// <param name="configJPath">A JSON path to select the configuration 
    /// section in the JSON file.</param>
    /// <param name="logFileName">The log file path.</param>
    private static void LoadConfiguration(ScriptPlayer scriptPlayer,
                                          string configFileName,
                                          string configJPath,
                                          string logFileName)
    {
      WriteAction($"load configuration from {configFileName}.", logFileName);
      scriptPlayer.LoadConfigFromJsonFile(configFileName, configJPath);
      WriteAction($"finish", logFileName);
    }

    /// <summary>
    /// Writes a message in the log file.
    /// </summary>
    /// <param name="fileName">The log file name.</param>
    /// <param name="message">The message test.</param>
    private static void Log(string fileName,
                            string message)
    {
      if (!string.IsNullOrEmpty(fileName))
        File.AppendAllText(fileName, message + "\n"
                         , System.Text.Encoding.Unicode);
    }

    /// <summary>
    /// Plays a system sound.
    /// </summary>
    private static void PlaySound()
    {

      if (ExitCode == 0)
        System.Media.SystemSounds.Asterisk.Play();
      else
        System.Media.SystemSounds.Hand.Play();
    }

    /// <summary>
    /// Reads a string from the console.
    /// </summary>
    /// <param name="hidden">If the value is true, the characters are not 
    /// output to the console.</param>
    /// <returns>The entered string.</returns>
    private static string ReadString(bool hidden = false)
    {
      if (!hidden)
        return Console.ReadLine();

      ConsoleKeyInfo keyInfo = Console.ReadKey(true);
      string value = "";

      while (keyInfo.Key != ConsoleKey.Enter)
      {
        if (keyInfo.Key == ConsoleKey.Backspace)
        {
          if (value.Length > 0)
            value = value.Remove(value.Length - 1, 1);
        }
        else
        {
          if (keyInfo.KeyChar >= 32)
            value += keyInfo.KeyChar;
        }

        keyInfo = Console.ReadKey(true);
      }

      Console.WriteLine();
      return value;
    }

    /// <summary>
    /// Resets the <see cref="Console"/> colors to the programm start values.
    /// </summary>
    private static void ResetColors()
    {
      Console.BackgroundColor = StandardColor[0];
      Console.ForegroundColor = StandardColor[1];
    }

    private static bool SavePlaceholderValue(string configFile,
                                             string configJPath,
                                             string placeholderName,
                                             string value,
                                             bool encrypted)
    {
      if (!string.IsNullOrEmpty(configFile)
       && !string.IsNullOrEmpty(configJPath)
       && !string.IsNullOrEmpty(placeholderName))
      {
        try
        {
          JObject config = JObject.Parse(File.ReadAllText(configFile));
          JToken playerConfig = config.SelectToken(configJPath);

          if (playerConfig == null)
          {
            WriteError($"Invalid JSON path {configJPath}");

            return false;
          }

          if (playerConfig["placeholders"] == null)
            playerConfig["placeholders"] = new JObject();

          JObject placeholders = playerConfig["placeholders"] as JObject;

          if (encrypted)
          {
            JObject placeholder = new();
            placeholder["encrypted"] = true;
            placeholder["value"] = value;
            placeholders[placeholderName] = placeholder;
          }
          else
            placeholders[placeholderName] = value;

          File.WriteAllText(configFile, config.ToString(Newtonsoft.Json.Formatting.Indented));

          return true;
        }
        catch (Exception exception)
        {
          WriteException(exception);
        }
      }

      return false;
    }

    /// <summary>
    /// Writes a string to the default output stream.
    /// </summary>
    /// <param name="value">The string to write.</param>
    /// <param name="colors">A color pair for background and foreground.
    /// </param>
    private static void Write(string value,
                              ConsoleColor[] colors)
    {
      Console.BackgroundColor = colors[0];
      Console.ForegroundColor = colors[1];

      Console.Write(value);
    }

    /// <summary>
    /// Writes an information text about an action ("load file ...") in the 
    /// output stream and the log file.
    /// </summary>
    /// <param name="value">The action description text.</param>
    /// <param name="logFileName">The log file name.</param>
    private static void WriteAction(string value,
                                    string logFileName)
    {
      lock (LockObject)
      {
        value = "-- " + value;

        WriteLine(value, ActionColor);
        Log(logFileName, value);
      }
    }

    /// <summary>
    /// Writes an argument in the output stream and the log file.
    /// </summary>
    /// <param name="value">The script command text.</param>
    /// <param name="logFileName">The log file name.</param>
    private static void WriteArgument(string name,
                                      string value,
                                      string logFileName)
    {
      lock (LockObject)
      {
        Write($"-- {name}: ", CommandColor);
        WriteLine(value, ArgumentColor);
        Log(logFileName, $"-- {name}: {value}");
      }
    }

    /// <summary>
    /// Outputs the arguments values.
    /// </summary>
    /// <param name="arguments">A filled <see cref="CommandLineArgs"/> struct 
    /// variable.</param>
    private static void WriteArguments(CommandLineArgs arguments)
    {
      WriteComment($"command line arguments", arguments.LogFile);

      if (arguments.EncryptString)
      {
        WriteArgument("encrypt string", "true", arguments.LogFile);
        WriteArgument("config file", arguments.ConfigFile, arguments.LogFile);
        WriteArgument("config path", arguments.ConfigJPath, arguments.LogFile);
        WriteArgument("placeholder name", arguments.placeholderName
                    , arguments.LogFile);

        return;
      }

      WriteArgument("script path", arguments.ScriptFile, arguments.LogFile);

      if (arguments.Build)
      {
        WriteArgument("build file", arguments.BuildFile, arguments.LogFile);
      }
      else
      {
        WriteArgument("config file", arguments.ConfigFile, arguments.LogFile);
        WriteArgument("config path", arguments.ConfigJPath, arguments.LogFile);
      }

      WriteArgument("log file", arguments.LogFile, arguments.LogFile);

      if (arguments.ReducedOutput)
        WriteArgument("reduced output"
                    , arguments.ReducedOutput.ToString().ToLower()
                    , arguments.LogFile);

      if (arguments.Alarm)
        WriteArgument("play sound is finish"
                    , arguments.Alarm.ToString().ToLower()
                    , arguments.LogFile);

      if (arguments.DeleteLog)
        WriteArgument("delete log after success run"
                    , arguments.DeleteLog.ToString().ToLower()
                    , arguments.LogFile);

      if (!arguments.Build && arguments.MonInterval > 0)
        WriteArgument("monitor interval", arguments.MonInterval.ToString()
                    , arguments.LogFile);

      if (arguments.SilentMode)
        WriteArgument("silent mode", arguments.SilentMode.ToString().ToLower()
                    , arguments.LogFile);
    }

    /// <summary>
    /// Writes a SQL command in the output stream and the log file.
    /// </summary>
    /// <param name="value">The SQL command text.</param>
    /// <param name="logFileName">The log file name.</param>
    /// <param name="logFileOnly">Writes the command only in the log file, 
    /// not in the output stream.</param>
    private static void WriteCommand(string value,
                                     string logFileName,
                                     bool logFileOnly = false)
    {
      lock (LockObject)
      {
        value = value?.Trim();

        if (!logFileOnly)
          WriteLine(value, CommandColor);

        Log(logFileName, value);
      }
    }

    /// <summary>
    /// Writes a standard comment in the output stream and the log file.
    /// </summary>
    /// <param name="value">The comment text.</param>
    /// <param name="logFileName">The log file name.</param>
    /// <param name="logFileOnly">Writes the comment only in the log file, 
    /// not in the output stream.</param>
    private static void WriteComment(string value,
                                     string logFileName,
                                     bool logFileOnly = false)
    {
      lock (LockObject)
      {
        value = $"-- {value.Replace("\n", "\n-- ")}";

        if (!logFileOnly)
          WriteLine(value, CommentColor);

        Log(logFileName, value);
      }
    }

    /// <summary>
    /// Writes an error message in the output stream and the log file.
    /// </summary>
    /// <param name="value">The error message.</param>
    /// <param name="logFileName">The log file name.</param>
    private static void WriteError(string value,
                                   string logFileName = null)
    {
      lock (LockObject)
      {
        value = $"-- error: {value.Replace("\n", "\n-- ")}";

        Write(value, ErrorColor);
        ResetColors();
        Console.WriteLine();
        Log(logFileName, value);
      }
    }

    /// <summary>
    /// Writes the message of an exception and all inner exceptions in the 
    /// output stream and the log file.
    /// </summary>
    /// <param name="value">The error message.</param>
    /// <param name="logFileName">The log file name.</param>
    private static void WriteException(Exception exception,
                                       string logFileName = null)
    {
      WriteError(exception.Message, logFileName);

      if (exception.InnerException != null)
        WriteException(exception.InnerException, logFileName);
    }

    /// <summary>
    /// Writes a string with a final line break to the standard output stream.
    /// </summary>
    /// <param name="value">The string to write.</param>
    /// <param name="colors">A color pair for background and foreground.
    /// </param>
    private static void WriteLine(string value,
                                  ConsoleColor[] colors)
    {
      Console.BackgroundColor = colors[0];
      Console.ForegroundColor = colors[1];

      Console.WriteLine(value);

      ResetColors();
    }

    /// <summary>
    /// Writes a message from the database server in the output stream and the 
    /// log file.
    /// </summary>
    /// <param name="value">The database server message.</param>
    /// <param name="logFileName">The log file name.</param>
    /// <param name="logFileOnly">Writes the comment only in the log file, 
    /// not in the output stream.</param>
    private static void WriteMessage(string value,
                                     string logFileName,
                                     bool logFileOnly = false)
    {
      lock (LockObject)
      {
        if (value.StartsWith("mon: "))
          value = $"-- {value.Replace("\n", "\n-- mon: ")}";
        else
          value = $"-- out: {value.Replace("\n", "\n-- out: ")}";

        if (!logFileOnly)
          WriteLine(value, MessageColor);

        Log(logFileName, value);
      }
    }

    /// <summary>
    /// Outputs the program name, version and copyright notice.
    /// </summary>
    /// <param name="logFileName">The log file name.</param>
    private static void WriteProgrammTitle(string logFileName)
    {
      WriteComment(new string('-', 77), logFileName);
      WriteComment($"FBSQL Command Line Script Player, "
                 + $"Version {GetAssemblyVersion()} "
                 + "\n© 2017-2022, D. Striebing"
                 , logFileName);
      WriteComment(new string('-', 77), logFileName);
    }

    /// <summary>
    /// Writes a script command in the output stream and the log file.
    /// </summary>
    /// <param name="value">The script command text.</param>
    /// <param name="logFileName">The log file name.</param>
    private static void WriteScriptCommand(string value,
                                           string logFileName)
    {
      lock (LockObject)
      {
        Write($"-- script command: ", CommentColor);

        value = value.Trim();

        WriteLine(value, CommandColor);
        Log(logFileName, $"-- script command: {value}");
      }
    }

    /// <summary>
    /// Writes the script execute summary.
    /// </summary>
    /// <param name="scriptTitle">The title of the script file.</param>
    /// <param name="logFileName">The log file name.</param>
    private static void WriteScriptEnd(string scriptTitle, string logFileName)
    {
      lock (LockObject)
      {
        Log(logFileName, new string('-', 80));
        WriteLine(new string('-', 80), StandardColor);
        WriteComment($"elapsed time for script: {scriptTitle}", logFileName);
        WriteComment($"script end: {DateTime.Now:o}"
                   , logFileName);
      }
    }

    /// <summary>
    /// Writes the script start informations.
    /// </summary>
    /// <param name="logFileName">The log file name.</param>
    private static void WriteScriptStart(string logFileName)
    {
      lock (LockObject)
      {
        Log(logFileName, new string('-', 80));
        WriteLine(new string('-', 80), StandardColor);
        WriteComment($"script start: {DateTime.Now:o}"
                   , logFileName);
      }
    }

    /// <summary>
    /// Writes the tree path of a script part in the output stream and the log 
    /// file.
    /// </summary>
    /// <param name="type">The script part type ("batch" or "step").</param>
    /// <param name="value">The script part tree path.</param>
    /// <param name="logFileName">The log file name.</param>
    private static void WriteTreePath(string type,
                                      string value,
                                      string logFileName)
    {
      lock (LockObject)
      {
        Write($"-- {type}".PadRight(9), StepTypeColor);
        WriteLine(value, TreePathColor);
        Log(logFileName, $"-- {type}".PadRight(9) + value);
      }
    }

    /// <summary>
    /// Writes a warning text in the output stream and the log file.
    /// </summary>
    /// <param name="value">The warning text.</param>
    /// <param name="logFileName">The log file name.</param>
    private static void WriteWarning(string value,
                                     string logFileName)
    {
      lock (LockObject)
      {
        value = $"-- warning: {value.Replace("\n", "\n-- ")}";

        WriteLine(value, WarningColor);
        Log(logFileName, value);
      }
    }

    /// <summary>
    /// FBSQL Command Line Script Player
    /// </summary>
    /// <param name="args">The command line arguments as a string array.
    /// </param>
    /// <returns>The exit code for the program.</returns>
    static int Main(string[] args)
    {
      Arguments = GetArguments(args);

      InitLogFile(Arguments.LogFile);
      WriteProgrammTitle(Arguments.LogFile);

      if (Arguments.ShowHelp)
        Help();

      WriteArguments(Arguments);

      if (Arguments.EncryptString)
      {
        EncryptEnteredString(Arguments.ConfigFile, Arguments.ConfigJPath
                           , Arguments.placeholderName);

        return 0;
      }

      if (string.IsNullOrEmpty(Arguments.ScriptFile))
        return 0;

      if (Arguments.Build)
      {
        BuildFile(Arguments.ScriptFile, Arguments.BuildFile
                , Arguments.LogFile);
      }
      else
      {
        using (PlayerConnection connection = new())
        {
          try
          {
            ScriptPlayer scriptPlayer = new(connection, HandlePlayerAction
                                          , DecryptString);

            LoadScriptFile(scriptPlayer, Arguments.ScriptFile
                         , Arguments.LogFile);
            LoadConfiguration(scriptPlayer, Arguments.ConfigFile
                            , Arguments.ConfigJPath, Arguments.LogFile);

            if (!Arguments.SilentMode)
            {
              Console.WriteLine();
              Console.WriteLine("Press any key to continue");
              Console.ReadLine();
              Console.WriteLine("...");
            }

            scriptPlayer.MonitorInterval = Arguments.MonInterval;

            scriptPlayer.Exec();

            ExitCode = scriptPlayer.ExitCode;
          }
          catch (Exception exception)
          {
            WriteException(exception, Arguments.LogFile);
          }
        }

        WriteComment($"program exit code: {ExitCode}", Arguments.LogFile);

        if (!string.IsNullOrEmpty(Arguments.LogFile))
          WriteComment($"log {new Uri(Arguments.LogFile).AbsoluteUri}", 
            Arguments.LogFile);

        try
        {
          if (!string.IsNullOrEmpty(Arguments.LogFile)
            && Arguments.DeleteLog && ExitCode == 0)
            if (File.Exists(Arguments.LogFile))
              File.Delete(Arguments.LogFile);
        }
        catch
        {
          // nothing to do
        }
      }

      if (Arguments.Alarm)
      {
        PlaySound();
      }

      if (!Arguments.SilentMode)
      {
        ResetColors();
        Console.WriteLine();
        Console.WriteLine("Press any key to exit");
        Console.ReadLine();
      }

      return ExitCode;
    }

  }

}