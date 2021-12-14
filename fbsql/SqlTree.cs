using System;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;


/// 2006-07-05 - created
/// 2021-12-14 - porting to .NET Standard 2.0

namespace Fahbing.Sql
{
  /// <summary>
  /// <see cref="SqlTree"/> is a descendant from <see cref="SqlTreeBatch"/>.
  /// </summary>
  /// <created>2006-07-05</created><modified>2015-02-07</modified>
  public class SqlTree : SqlTreeBatch
  {
    /// <summary>The build number of the script.</summary>
    public int Build { get; private set; }

    /// <summary>The loaded script as an XML document.</summary>
    protected XDocument Document { get; private set; }

    /// <summary>The major version of the script.</summary>
    public int Major { get; set; }

    /// <summary>The minor version of the script.</summary>
    public int Minor { get; set; }

    /// <summary>The release number of the script.</summary>
    public int Release { get; set; }


    /// <summary>
    /// Creates a new instance of the <see cref="SqlTree"/> class.
    /// </summary>
    /// <created>2012-06-21</created><modified>2021-12-14</modified>
    public SqlTree() : base(null)
    {
      Reset();
    }

    /// <summary>
    /// Gets a new empty script as <see cref="XDocument"/> instance.
    /// </summary>
    /// <returns>A <see cref="XDocument"/> instance that contains a new empty 
    /// script.</returns>
    /// <created>2006-07-05</created><modified>2021-12-14</modified>
    public XDocument GetEmptyXDocument()
    {
      XElement batch = new("batch"
                         , new XAttribute("name", "script")
                         , new XAttribute("executable", "true")
                         , new XAttribute("expanded", "true")
                         , new XAttribute("guid"
                         , System.Guid.NewGuid().ToString().ToLower()));
      XElement version = new("version"
                           , new XAttribute("major", "0")
                           , new XAttribute("minor", "0")
                           , new XAttribute("release", "0")
                           , new XAttribute("build", "0"));
      XElement script = new("script"
                           , new XAttribute("version", "3.0.0")
                           , new XAttribute("sqllanguage", "")
                           , new XAttribute("guid"
                           , System.Guid.NewGuid().ToString().ToLower())
                           , version, batch);
      return new XDocument(script);
    }

    /// <summary>
    /// Gets the current script as an <see cref="XDocument"/> instance.
    /// </summary>
    /// <returns>An <see cref="XDocument"/> instance that contains the 
    /// complete script.</returns>
    /// <created>2006-07-05</created><modified>2021-12-14</modified>
    public XDocument GetXDocument()
    {
      XDocument document = GetEmptyXDocument();
      XElement scriptNode = document.Element("script");
      XElement batchNode = scriptNode.Element("batch");

      scriptNode.Attribute("guid").SetValue(Guid);
      scriptNode.Element("version").Attribute("major").SetValue(Major);
      scriptNode.Element("version").Attribute("minor").SetValue(Minor);
      scriptNode.Element("version").Attribute("release").SetValue(Release);
      scriptNode.Element("version").Attribute("build").SetValue(Build);

      batchNode.ReplaceWith(GetXElement());

      return document;
    }

    /// <summary>
    /// Gets the version numbers from a version <see cref="XElement"/>.
    /// </summary>
    /// <param name="version"></param>
    /// <created>2012-06-21</created><modified>2021-12-14</modified>
    private void GetVersionFromXElement(XElement version)
    {
      Major = GetInt32FromAttribute(version.Attribute("major"));
      Minor = GetInt32FromAttribute(version.Attribute("minor"));
      Release = GetInt32FromAttribute(version.Attribute("release"));
      Build = GetInt32FromAttribute(version.Attribute("build"));
    }

    /// <summary>
    /// Increments the script build number.
    /// </summary>
    /// <created>2006-07-05</created><modified>2021-12-14</modified>
    private void IncBuildNumber()
    {
      Build += 1;
    }

    /// <summary>
    /// Loads a script from a directory.
    /// </summary>
    /// <param name="path">A file system path to the directory.</param>
    /// <param name="action">A <see cref="LoadAction"/> delegate function, 
    /// e.B. for progress indicators.</param>
    /// <created>2012-06-21</created><modified>2021-12-14</modified>
    public override void LoadFromDirectory(string path
                                         , LoadAction action = null)
    {
      Reset();

      path = IncludeTrailingPathDelimiter(path);
      string scriptPath = Path.Combine(path, "script");

      if (!Directory.Exists(path))
        return;
      if (!Directory.Exists(scriptPath))
        return;

      GetVersionFromXElement(XDocument.Load(Path.Combine(scriptPath
                           , "__version.xml")).Element("version"));
      base.LoadFromDirectory(scriptPath, action);

      Document = GetXDocument();
    }

    /// <summary>
    /// Loads a script from an XML file.
    /// </summary>
    /// <param name="path">A file system path to the file to load.</param>
    /// <created>2012-06-21</created><modified>2021-12-14</modified>
    public void LoadFromXmlFile(string fileName)
    {
      Reset();

      Document = XDocument.Load(fileName);
      XElement scriptNode = Document.Element("script");
      XElement batchNode = scriptNode.Element("batch");

      GetVersionFromXElement(scriptNode.Element("version"));
      LoadFromXElement(batchNode);
    }

    /// <summary>
    /// Resets the script.
    /// </summary>
    /// <created>2015-02-08</created><modified>2021-12-14</modified>
    public override void Reset()
    {
      Document = null;
      Major = 1;
      Minor = 0;
      Release = 0;
      Build = 0;

      base.Reset();
    }

    /// <summary>
    /// Saves a script to a directory (folder script).
    /// </summary>
    /// <param name="path">A file system path to the destination directory.
    /// </param>
    /// <created>2012-06-21</created><modified>2021-12-14</modified>
    public override void SaveToDirectory(string path)
    {
      string backupPath = Path.Combine(path, "__backup");
      string historyPath = Path.Combine(path, "__history");
      string scriptPath = Path.Combine(path, "script");

      Directory.CreateDirectory(backupPath);
      Directory.CreateDirectory(historyPath);

      if (Directory.Exists(scriptPath))
      {
        Document.Save(Path.Combine(historyPath
                    , $"Build_{Build.ToString().PadLeft(6, '0')}.xss"));

        Directory.Move(scriptPath, Path.Combine(backupPath
                     , Build.ToString().PadLeft(6, '0') + "_"
                     + DateTime.Now.ToFileTime().ToString()));

        //if (Directory.Exists(path))
        //  ZipFile.CreateFromDirectory(scriptPath, Path.Combine(path
        //        , $"Build_{Build.ToString().PadLeft(6, '0')}.zip"));
      }

      IncBuildNumber();

      Directory.CreateDirectory(scriptPath);
      new XElement("version",
                   new XAttribute("major", Major.ToString()),
                   new XAttribute("minor", Minor.ToString()),
                   new XAttribute("release", Release.ToString()),
                   new XAttribute("build", Build.ToString())
                  ).Save(Path.Combine(scriptPath, "__version.xml"));

      base.SaveToDirectory(scriptPath);

      Document = GetXDocument();

      Document.Save(Path.Combine(path, "script.xss"));
    }

    /// <summary>
    /// Saves a script to an XML file (.xss - Xml Sql Script).
    /// </summary>
    /// <param name="fileName">A file system path to the destination file.
    /// </param>
    /// <created>2006-07-05</created><modified>2021-12-14</modified>
    public void SaveToXmlFile(string fileName)
    {
      IncBuildNumber();
      GetXDocument().Save(fileName);
    }
  }

}
