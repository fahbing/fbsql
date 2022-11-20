using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;


/// 2006-07-05 - created
/// 2015-02-07 - porting to c#
/// 2021-12-14 - adaptations for .NET Standard 2.0

namespace Fahbing.Sql
{
  /// <summary>
  /// Represents one or more SQL commands.
  /// </summary>
  /// <created>2006-07-05</created><changed>2022-11-20</changed>
  public class SqlTreeStep : SqlTreeItem
  {
    /// <summary>Gets or sets the source file name as debug information.
    /// </summary>
    public string SourcePath { get; private set; }

    /// <summary>Gets or sets the SQL command text.</summary>
    public string Sql { get; set; }


    /// <summary>
    /// Creates a new instance of the <see cref="SqlTreeStep"/> class.
    /// </summary>
    /// <param name="parent">The parent <see cref="SqlTreeBatch"/> instance.</param>
    /// <created>2006-07-05</created><changed>2022-08-19</changed>
    public SqlTreeStep(SqlTreeBatch parent)
      : base(SqlTreeItemType.step, parent)
    {
      Executable = true;
      SourcePath = "";
      Sql = "";

      if (this.Parent != null)
        parent.Add(this);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="SqlTreeStep"/> class.
    /// </summary>
    /// <param name="step">The step definition as a <see cref="XElement"/> 
    /// instance.</param>
    /// <param name="parent">The parent <see cref="SqlTreeBatch"/> instance.
    /// </param>
    /// <created>2006-07-05</created><changed>2022-11-19</changed>
    public SqlTreeStep(XElement step,
                       SqlTreeBatch parent)
      : this(parent)
    {
      Title = GetStringFromXAttr(step.Attribute("name"));
      Executable = GetBooleanFromXAttr(step.Attribute("executable"));
      CompLevelCondition = GetCompLevelCondFromXAttr(step.Attribute("tsql_compLevel"));
      Sql = step.Value;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="SqlTreeStep"/> class.
    /// </summary>
    /// <param name="title">The title for the new <see cref="SqlTreeStep"/> 
    /// instance.</param>
    /// <param name="sql">The SQL command(s) for the new <see 
    /// cref="SqlTreeStep"/> instance.</param>
    /// <param name="parent">The parent <see cref="SqlTreeBatch"/> instance.
    /// </param>
    /// <created>2006-07-05</created><changed>2015-02-07</changed>
    public SqlTreeStep(string title,
                       string sql,
                       SqlTreeBatch parent)
      : this(parent)
    {
      this.Title = title;
      this.Sql = sql;
    }

    /// <summary>
    /// Clones the step and returns a new <see cref="SqlTreeStep"/> 
    /// instance with the same properties.
    /// </summary>
    /// <returns>A new <see cref="SqlTreeStep"/> instance.</returns>
    /// <created>2015-07-11</created><changed>2022-05-26</changed>
    public override object Clone()
    {
      return new SqlTreeStep(this.GetXElement(), null);
    }

    /// <summary>
    /// Gets a <see cref="List&lt;SqlScriptCommand&gt;"/> instance, that 
    /// contains the splitted SQL commands.
    /// </summary>
    /// <param name="parser">
    /// A <see cref="StringParser"/> instance that is passed the SQL to parse.
    /// </param>
    /// <returns>A <see cref="List&lt;SqlScriptCommand&gt;"/> instance.</returns>
    /// <created>2015-07-11</created><changed>2015-07-11</changed>
    public List<SqlScriptCommand> GetCommands(StringParser parser)
    {
      return SqlScriptCommand.Parse(parser, Sql, SourcePath);
    }

    /// <summary>
    /// Gets the defintion of this step as an <see cref="XElement"/>.
    /// </summary>
    /// <returns>An <see cref="XElement"/> that represents the definition of 
    /// this step.</returns>
    /// <created>2015-02-07</created><changed>2022-11-20</changed>
    public override XElement GetXElement()
    {
      XElement result = new("step", new XAttribute("name", Title)
                          , new XAttribute("executable"
                          , Executable.ToString().ToLower())
                          , new XText(Sql ?? ""));

      if (CompLevelCondition != null)
        result.Add(new XAttribute("tsql_compLevel"
                 , CompLevelCondition.ToString()));
      
      return result;
    }

    /// <summary>
    /// Loads a <see cref="SqlTreeStep"/> from an <see cref="XElement"/> 
    /// instance.
    /// </summary>
    /// <param name="step">An <see cref="XElement"/> instance that contains 
    /// the definition of the <see cref="SqlTreeStep"/>.</param>
    /// <created>2015-02-08</created><changed>2022-11-19</changed>
    protected void LoadFromXElement(XElement step)
    {
      Title = GetStringFromXAttr(step.Attribute("name"));
      Executable = GetBooleanFromXAttr(step.Attribute("executable"));
      CompLevelCondition = GetCompLevelCondFromXAttr(step.Attribute("tsql_compLevel"));
    }

    /// <summary>
    /// Load the step content from a file.
    /// </summary>
    /// <param name="path">A file system path to the file.</param>
    /// <param name="action">A <see cref="LoadAction"/> delegate function, 
    /// e.B. for progress indicators.</param>
    /// <param name="debug">Specifies whether debug information should be 
    /// stored.</param>
    /// <created>2015-02-08</created><modified>2022-11-20</modified>
    public override void LoadFromDirectory(string path
                                         , LoadAction action = null
                                         , bool debug = false)
    {
      if (File.Exists(path))
      {
        action?.Invoke(SqlTreeItemType.step, path);

        if (debug)
          SourcePath = Path.GetFullPath(path);

        var lines = File.ReadAllLines(path);

        if (lines.Length > 0)
        {
          int offset = 0;
          Regex regEx = new(@"^\s*--\s*(\<step [^\>]+\>)");

          if (regEx.IsMatch(lines[0]))
          {
            LoadFromXElement(XElement.Parse(regEx.Match(lines[0]).Groups[1].Value));
            offset++;

            if (Title == "")
              Title = GetTitleFromFileName(path);
          }
          else
            LoadPropsFromFileName(path);

          for (int index = offset; index < lines.Length; index++)
            Sql += lines[index] + Environment.NewLine;
        }
        else
          LoadPropsFromFileName(path);
      }
      else
        Reset();
    }

    /// <summary>
    /// Gets the file name as the title.
    /// </summary>
    /// <param name="fileName">A file system path to the source file.</param>
    /// <returns>The title as string.</returns>
    /// <created>2015-02-08</created><modified>2022-11-19</modified>
    private string GetTitleFromFileName(string fileName)
    {
      string result = Path.GetFileNameWithoutExtension(fileName);

      return Regex.IsMatch(result, "^\\d{4}-") ? result.Substring(5) : result;
    }

    /// <summary>
    /// Sets the file name as the title.
    /// </summary>
    /// <param name="fileName">A file system path to the source file.</param>
    /// <created>2015-02-08</created><modified>2021-12-12</modified>
    private void LoadPropsFromFileName(string fileName)
    {
      Reset();

      Title = GetTitleFromFileName(fileName);
    }

    /// <summary>
    /// Saves the step as a file.
    /// </summary>
    /// <param name="fileName">A file system path to the destination file.
    /// </param>
    /// <created>2015-02-07</created><modified>2021-12-29</modified>
    public override void SaveToDirectory(string fileName)
    {
      var xmlStep = new XElement("step"
                  , new XAttribute("name", Title)
                  , new XAttribute("executable"
                  , Executable.ToString().ToLower()));

      File.WriteAllText(fileName
                      , "-- " + xmlStep.ToString()
                      + Environment.NewLine + Sql);
    }
  }

}