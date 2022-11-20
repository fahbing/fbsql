using System;
using System.Collections.Generic;


/// 1999-04-24 - created
/// 2017-06-13 - porting to c#
/// 2021-12-14 - adaptations for .NET Standard 2.0

namespace Fahbing.Sql
{
  /// <summary>
  /// A delegate function definition to receive informations while loading a 
  /// script.
  /// </summary>
  /// <param name="type">The type of a script node <see cref=
  /// "SqlTreeItemType.batch"/> or <see cref="SqlTreeItemType.step"/>.</param>
  /// <param name="path">The file system path of a source for a batch or step.
  /// </param>
  public delegate void LoadScriptItemAction(SqlTreeItemType type, string path);


  /// <summary>
  /// Defines the various script command types.
  /// </summary>
  /// <created>2014-11-29</created><modified>2021-05-10</modified>
  public enum SqlScriptCmdType
  {
    /// <summary>Script command to output a comment.</summary>
    comment,
    /// <summary>Script command to commit a transaction.</summary>
    commit,
    /// <summary>Script command to connect to a database server.</summary>
    connect,
    /// <summary>Script command to close an open connection.</summary>
    disconnect,
    /// <summary>Script command to rollback a transaction.</summary>
    rollback,
    /// <summary>Script command to set a new script exit code returned by the 
    /// script player in silent mode. If the script runs completely, 0 is 
    /// always returned.</summary>
    setExitCode,
    /// <summary>Script command to set a new placeholder value.</summary>
    setPlaceholder,
    /// <summary>Script command to set a new command timeout.</summary>
    setTimeout,
    /// <summary>Script command to set a new terminator.</summary>
    setTerminator,
    /// <summary>SQL command to execute.</summary>
    sql,
    /// <summary>Script command to start a transaction.</summary>
    startTransaction,
    /// <summary>Script command to stop a transaction.</summary>
    stopTransaction,
  };


  /// <summary>
  /// Represents a single SQL or scriptcommand.
  /// </summary>
  /// <created>2014-11-29</created><modified>2021-03-13</modified>
  public class SqlScriptCommand
  {
    /// <summary>The command type.</summary>
    public SqlScriptCmdType CmdType { get; private set; }

    /// <summary>Optional parameters for the command.</summary>
    public object[] CmdParams { get; private set; }

    /// <summary>The source file name as debug information.</summary>
    public DebugInfo DebugInfo { get; private set; }

    /// <summary>The SQL command text.</summary>
    public string Sql { get; private set; }

    /// <summary>The step title of the command.</summary>
    public string Title { get; private set; }


    /// <summary>
    /// Creates a new instance of <see cref="SqlScriptCommand"/>.
    /// </summary>
    /// <param name="sql">The SQL command text.</param>
    /// <created>2014-11-29</created><modified>2021-03-13</modified>
    public SqlScriptCommand(string sql)
        : this(sql, SqlScriptCmdType.sql, null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="SqlScriptCommand"/>.
    /// </summary>
    /// <param name="sql">The SQL command text.</param>
    /// <param name="sourcePath">The source file name as debug information.
    /// </param>
    /// <created>2022-08-19</created><modified>2022-08-19</modified>
    public SqlScriptCommand(string sql, string sourcePath)
        : this(sql, SqlScriptCmdType.sql, null)
    {
      DebugInfo = new(sourcePath);
    }

    /// <summary>
    /// Creates a new instance of <see cref="SqlScriptCommand"/>.
    /// </summary>
    /// <param name="cmdType">The type of the command.</param>
    /// <created>2014-11-29</created><modified>2021-03-13</modified>
    public SqlScriptCommand(SqlScriptCmdType cmdType)
        : this("", cmdType, null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="SqlScriptCommand"/>.
    /// </summary>
    /// <param name="cmdType">The type of the command.</param>
    /// <param name="cmdParams">The command parameters.</param>
    /// <created>2014-11-29</created><modified>2021-03-13</modified>
    public SqlScriptCommand(SqlScriptCmdType cmdType,
                            object[] cmdParams)
        : this("", cmdType, cmdParams)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="SqlScriptCommand"/>.
    /// </summary>
    /// <param name="sql">The SQL command text.</param>
    /// <param name="cmdType">The type of the command.</param>
    /// <param name="cmdParams">The command parameters.</param>
    /// <created>2014-11-29</created><modified>2021-03-13</modified>
    public SqlScriptCommand(string sql,
                            SqlScriptCmdType cmdType,
                            object[] cmdParams)
    {
      Sql = sql;
      CmdType = cmdType;
      CmdParams = cmdParams;
    }

    /// <summary>
    /// Extracts the commands from a step.
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="value"></param>
    /// <returns>A List of <see cref="SqlScriptCommand"/> instances</returns>
    /// <created>1999-04-24</created><modified>2021-03-13</modified>
    public static List<SqlScriptCommand> Parse(StringParser parser,
                                               string value,
                                               string sourcePath = "")
    {
      List<SqlScriptCommand> commands = new();
      parser.Text = value;

      parser.NextToken();

      string command;
      int offset = parser.TokenOffset;
      bool isFirstToken = true;

      while (parser.TokenType != ParserTokenType.eof)
      {
        if (parser.TokenType == ParserTokenType.terminator)
        {
          command = value.Substring(offset, parser.TokenOffset - offset);
          isFirstToken = true;

          if (command.Trim() != "")
            commands.Add(new SqlScriptCommand(command, sourcePath));

          parser.NextToken();

          offset = parser.TokenOffset;
        }
        else
        {
          if (ParseSetCommand(parser, ref commands))
            offset = parser.TokenOffset;
          else if (isFirstToken && ParseScriptCommand(parser, ref commands))
            offset = parser.TokenOffset;
          else
            parser.NextToken();

          isFirstToken = false;
        }
      }

      command = value.Substring(offset, parser.TokenOffset - offset);

      if (command.Trim() != "")
        commands.Add(new SqlScriptCommand(command, sourcePath));

      return commands;
    }

    /// <summary>
    /// Checks whether the current parser position points to a CONNECT command. 
    /// If so, it is added to the <paramref name="commands"/> as <see 
    /// cref="SqlScriptCommand"/> instance, the parser is positioned on the 
    /// next token after the CONNECT statement. 
    /// </summary>
    /// <param name="parser">A <see cref="StringParser"/> instance which 
    /// contains the commands.</param>
    /// <param name="commands">A reference to a <see cref="SqlScriptCommand"/> 
    /// list.</param>
    /// <returns>If a CONNECT command is detected, the method returns <see 
    /// langword="true"/>, otherwise <see langword="false"/>.</returns>
    /// <created>1999-04-24</created><modified>2021-02-14</modified>
    private static bool ParseConnectCommand(StringParser parser,
                                            ref List<SqlScriptCommand> commands)
    {
      if (parser.isSymbol("connect"))
      {
        parser.NextToken();

        if (parser.TokenSubtype != ParserTokenSubtype.stringSingleQuotes)
          parser.raiseException(ParserTokenSubtype.stringSingleQuotes);

        var cmdParams = new object[1];
        cmdParams[0] = parser.toString();

        commands.Add(new SqlScriptCommand(SqlScriptCmdType.connect, cmdParams));
        parser.NextToken();

        // for backward compatibility
        if (parser.ToString() == ";" && parser.Terminator != ";")
          parser.NextToken();

        return true;
      }

      return false;
    }

    /// <summary>
    /// Checks whether the current parser position points to a script command. 
    /// Script commands include: CONNECT, DISCONNECT, COMMIT, ROLLBACK,
    /// START TRANSACTION and STOP TRANSACTION.
    /// If so, it is added to the <paramref name="commands"/> as <see 
    /// cref="SqlScriptCommand"/> instance, the parser is positioned on the 
    /// next token after the script command statement. 
    /// </summary>
    /// <param name="parser">A <see cref="StringParser"/> instance which 
    /// contains the commands.</param>
    /// <param name="commands">A reference to a <see cref="SqlScriptCommand"/> 
    /// list.</param>
    /// <returns></returns>
    /// <created>1999-04-24</created><modified>2021-03-13</modified>
    private static bool ParseScriptCommand(StringParser parser
                                         , ref List<SqlScriptCommand> commands)
    {
      if (parser.isSymbol("starttransaction")
       || parser.isSymbol("stoptransaction")
       || parser.isSymbol("commit")
       || parser.isSymbol("rollback")
       || parser.isSymbol("disconnect"))
      {
        commands.Add(new SqlScriptCommand((SqlScriptCmdType)Enum.Parse(typeof(SqlScriptCmdType)
                   , parser.toString(), true)));
        parser.NextToken();

        // for backward compatibility
        if (parser.ToString() == ";" && parser.Terminator != ";")
          parser.NextToken();

        return true;
      }
      else if (ParseConnectCommand(parser, ref commands))
        return true;

      return false;
    }

    /// <summary>
    /// Checks whether the current parser position points to a script SET 
    /// command. A Script SET commands include: SET TERM, SET TIMEOUT and SET 
    /// PLACEHOLDER.
    /// If so, it is added to the <paramref name="commands"/> as <see 
    /// cref="SqlScriptCommand"/> instance, the parser is positioned on the 
    /// next token after the script SET command statement. 
    /// </summary>
    /// <param name="parser">A <see cref="StringParser"/> instance which 
    /// contains the commands.</param>
    /// <param name="commands">A reference to a <see cref="SqlScriptCommand"/> 
    /// list.</param>
    /// <returns></returns>
    /// <created>1999-04-24</created><modified>2021-12-12</modified>
    private static bool ParseSetCommand(StringParser parser
                                      , ref List<SqlScriptCommand> commands)
    {
      if (parser.isSymbol("SET"))
      {
        var bookmark = parser.getBookmark();

        parser.NextToken();

        if (ParseSetTermCommand(parser)
         || ParseSetTimeoutCommand(parser, ref commands)
         || ParseSetPlaceholderCommand(parser, ref commands))
          return true;

        parser.goToBookmark(bookmark);
      }

      return false;
    }


    /// <summary>
    /// Checks whether the current parser position points to a SET TERM 
    /// command. If so, it is added to the <paramref name="commands"/> as <see 
    /// cref="SqlScriptCommand"/> instance, the parser is positioned on the 
    /// next token after the SET TERM statement. 
    /// </summary>
    /// <param name="parser">A <see cref="StringParser"/> instance which 
    /// contains the commands.</param>
    /// <param name="ver1Fix"></param>
    /// <returns></returns>
    /// <exception cref="ApplicationException"></exception>
    /// <created>1999-04-24</created><modified>2017-07-14</modified>
    private static bool ParseSetTermCommand(StringParser parser,
                                            bool ver1Fix = true)
    {
      if (parser.isSymbol("term"))
      {
        parser.NextToken();

        if (parser.TokenType == ParserTokenType.@char)
        {
          var delimiterchar = parser.toString();
          var delimiter = delimiterchar;
          var tokenOffset = parser.TokenOffset;

          parser.NextToken();

          while (parser.TokenType == ParserTokenType.@char 
              && parser.TokenOffset == tokenOffset + 1)
          {
            delimiter += delimiterchar;
            tokenOffset++;
            parser.NextToken();
          }

          if (!ver1Fix)
            parser.Terminator = delimiter;
        }
        else if (parser.TokenType == ParserTokenType.symbol
              || parser.TokenType == ParserTokenType.terminator)
        {
          if (!ver1Fix)
            parser.Terminator = parser.toString();

          parser.NextToken();
        }
        else
          throw new ApplicationException("invalid delimiter");

        return true;
      }

      return false;
    }

    /// <summary>
    /// Checks whether the current parser position points to a SET TIMEOUT
    /// command. If so, it is added to the <paramref name="commands"/> as <see 
    /// cref="SqlScriptCommand"/> instance, the parser is positioned on the 
    /// next token after the SET TIMEOUT statement. 
    /// </summary>
    /// <param name="parser">A <see cref="StringParser"/> instance which 
    /// contains the commands.</param>
    /// <param name="commands">A reference to a <see cref="SqlScriptCommand"/> 
    /// list.</param>
    /// <returns></returns>
    /// <created>2008-03-26</created><modified>2021-02-14</modified>
    private static bool ParseSetTimeoutCommand(
                                           StringParser parser,
                                           ref List<SqlScriptCommand> commands)
    {
      if (parser.isSymbol("timeout"))
      {
        parser.NextToken();

        if (parser.TokenType != ParserTokenType.number)
          parser.raiseException(ParserTokenType.number);

        var cmdParams = new Object[1];
        cmdParams[0] = parser.toInt32();

        commands.Add(new SqlScriptCommand(SqlScriptCmdType.setTimeout, cmdParams));
        parser.NextToken();

        // for backward compatibility
        if (parser.ToString() == ";" && parser.Terminator != ";")
          parser.NextToken();

        return true;
      }

      return false;
    }

    /// <summary>
    /// Checks whether the current parser position points to a SET PLACEHLDER
    /// command. If so, it is added to the <paramref name="commands"/> as <see 
    /// cref="SqlScriptCommand"/> instance, the parser is positioned on the 
    /// next token after the SET PLACEHOLDER statement. 
    /// </summary>
    /// <remarks>In older versions, a PLACEHOLDER was called SCRIPTVAR.
    /// </remarks>
    /// <param name="parser">A <see cref="StringParser"/> instance which 
    /// contains the commands.</param>
    /// <param name="commands">A reference to a <see cref="SqlScriptCommand"/> 
    /// list.</param>
    /// <returns></returns>
    /// <created>1999-04-24</created><modified>2021-05-15</modified>
    private static bool ParseSetPlaceholderCommand(
                                           StringParser parser,
                                           ref List<SqlScriptCommand> commands)
    {
      if (parser.isSymbol("placeholder")
       || parser.isSymbol("scriptvar"))
      {
        parser.NextToken();

        if (parser.TokenType != ParserTokenType.symbol
         && parser.TokenSubtype != ParserTokenSubtype.stringSingleQuotes)
          parser.raiseException("Identifier in single quotes expected.");

        var cmdParams = new object[3];
        cmdParams[0] = parser.toString();

        parser.NextToken();

        if (parser.isSymbol("from"))
        {
          parser.NextToken();

          if (!parser.isSymbol("config"))
            parser.raiseException("Key word 'config' expected.");

          cmdParams[2] = "config";
        }
        else
        {
          if (parser.TokenType != ParserTokenType.@string)
            parser.raiseException("String or key word 'from' expected.");

          cmdParams[1] = ParsePlaceholderValue(parser);

          commands.Add(new SqlScriptCommand(SqlScriptCmdType.setPlaceholder, cmdParams));
        }

        parser.NextToken();

        // for backward compatibility
        if (parser.ToString() == ";" && parser.Terminator != ";")
          parser.NextToken();

        return true;
      }

      return false;
    }

    /// <summary>
    /// Returns the value of an in-script "SET PLACEHOLDER" command.
    /// </summary>
    /// <param name="parser">A <see cref="StringParser"/> instance which 
    /// contains the commands.</param>
    /// <returns>The value of the placeholder.</returns>
    /// <created>1999-04-24</created><modified>2022-11-20</modified>
    private static string ParsePlaceholderValue(StringParser parser)
    {
      if (parser.TokenType == ParserTokenType.symbol)
      {
        //if (parser.isSymbol("config"))
        //{
        //  //var placeholder = ScriptSection.getConfig().placeholders[(scriptId ?? "") + "@" + name];
        //  //return placeholder != null ? placeholder.value : "";
        //  return "";
        //}
        //else
        parser.raiseException(parser.toString());
      }

      if (parser.TokenSubtype != ParserTokenSubtype.stringSingleQuotes)
        parser.raiseException(ParserTokenSubtype.stringSingleQuotes);

      return parser.toString();
    }
  }

}
