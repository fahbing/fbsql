using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;


/// 2006-07-05 - created
/// 2015-02-07 - porting to c#
/// 2021-12-14 - .NET Standard 2.0

namespace Fahbing.Sql
{
  /// <summary>
  /// Defines a delegate function, which is called when loading a <see 
  /// cref="SqlTreeItem"/> and can be used e.g. for the progress indicators.
  /// </summary>
  /// <param name="type">The type of the SqlTreeItem instance, see <see 
  /// cref="SqlTreeItemType"/>.</param>
  /// <param name="path">A file system path to a file or directory</param>
  public delegate void LoadAction(SqlTreeItemType type, string path);


  /// <summary>
  /// Defines the types of <see cref="SqlTreeItem" /> objects.
  /// </summary>
  /// <created>2006-07-05</created><modified>2018-03-10</modified>
  public enum SqlTreeItemType
  {
    /// <summary>
    /// The <see cref="SqlTreeItem"/> is a batch object, that contains other 
    /// batch or step objects.
    /// </summary>
    batch,
    /// <summary>
    /// The <see cref="SqlTreeItem"/> is a step object, that contains SQL 
    /// commands.
    /// </summary>
    step
  }


  /// <summary>
  /// The base class for <see cref="SqlTreeStep"/>, <see cref="SqlTreeBatch"/> 
  /// and <see cref="SqlTree"/>.
  /// </summary>    
  /// <created>2006-07-05</created><modified>2018-03-10</modified>
  public abstract class SqlTreeItem : ICloneable
  {
    /// <summary>
    /// Indicates whether the item should be executed, if the script is 
    /// executed.
    /// </summary>
    public bool Executable { get; set; }

    /// <summary>
    /// Gets the type (<see cref="SqlTreeItemType"/>) for this item.
    /// </summary>
    public SqlTreeItemType ItemType { get; private set; }

    /// <summary>
    /// Gets the instance of the parent item (<see cref="SqlTreeBatch"/>).
    /// </summary>
    public SqlTreeBatch Parent { get; internal set; }

    /// <summary>
    /// Gets or sets the title of this item.
    /// </summary>
    public string Title { get; set; }


    /// <summary>
    /// Creates a new instance of the <see cref="SqlTreeItem"/> class.
    /// </summary>
    /// <param name="itemType">Defines the type for this item.</param>
    /// <param name="parent">
    /// The parent <see cref="SqlTreeBatch"/> instance.
    /// </param>
    /// <created>2006-07-05</created><changed>2021-12-15</changed>
    public SqlTreeItem(SqlTreeItemType itemType,
                       SqlTreeBatch parent)
    {
      ItemType = itemType;
      Parent = parent;

      Reset();
    }

    /// <summary>
    /// Clones the step and returns a new instance with the same properties.
    /// </summary>
    /// <returns>A new instance of a descendant of this class.</returns>
    /// <created>2015-07-11</created><changed>2018-03-11</changed>
    public abstract object Clone();

    /// <summary>
    /// Returns the value of an XML attribute as a Boolean.
    /// </summary>
    /// <param name="attr">The XML attribute as <see cref="XAttribute"/> 
    /// instance, which should be interpreted as Boolean.</param>
    /// <returns>The value of the attribute interpreted as Boolean.</returns>
    /// <created>2015-07-11</created><changed>2022-05-26</changed>
    protected bool GetBooleanFromXAttribute(XAttribute attr)
    {
      return attr != null && Convert.ToBoolean(attr.Value);
    }

    /// <summary>
    /// Gets the value of a <see cref="XAttribute"/> as a <see cref="int"/>.
    /// </summary>
    /// <param name="attr"></param>
    /// <returns>The value as a <see cref="int"/>.</returns>
    /// <created>2015-07-11</created><changed>2022-05-26</changed>
    protected int GetInt32FromAttribute(XAttribute attr)
    {
      return attr != null ? Convert.ToInt32(attr.Value) : 0;
    }

    /// <summary>
    /// Gets the path for this item. The parent objects are separated by a 
    /// slash.
    /// </summary>
    /// <returns>Returns the path for this item.</returns>
    /// <created>2014-07-23</created><changed>2022-05-26</changed>
    public virtual string GetPath()
    {
      return Parent != null ? Parent.GetPath() + "/" + Title : Title;
    }

    /// <summary>
    /// Gets the value of a <see cref="XAttribute"/> as a <see cref="string"/>.
    /// </summary>
    /// <param name="attr"></param>
    /// <returns>The value as a <see cref="string"/></returns>
    /// <created>2015-07-11</created><changed>2022-05-26</changed>
    protected string GetStringFromXAttribute(XAttribute attr)
    {
      return attr != null ? attr.Value : "";
    }

    /// <summary>
    /// Gets the defintion of this item as an <see cref="XElement"/>.
    /// </summary>
    /// <returns>An <see cref="XElement"/> that represents the definition of 
    /// this item.</returns>
    /// <created>2015-02-07</created><changed>2022-05-26</changed>
    public abstract XElement GetXElement();

    /// <summary>
    /// Ensures that a path ended without a directory separator.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <created>2015-02-07</created><changed>2021-12-12</changed>
    protected static string ExcludeTrailingPathDelimiter(string path)
    {
      if (path.EndsWith(Path.DirectorySeparatorChar.ToString()))
      {
        path = path.Remove(path.Length - 1);
      }

      return path;
    }

    /// <summary>
    /// Ensures that a path ended with a directory separator.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <created>2015-02-07</created><changed>2021-12-12</changed>
    protected static string IncludeTrailingPathDelimiter(string path)
    {
      if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
        path += Path.DirectorySeparatorChar;

      return path;
    }

    /// <summary>
    /// An abstract method to load the item content from a file or directory.
    /// </summary>
    /// <param name="path">A file system path to a file or directory</param>
    /// <param name="action">A <see cref="LoadAction"/> delegate function, 
    /// e.g. for progress indicators.</param>
    /// <created>2015-02-08</created><modified>2021-12-11</modified>
    public abstract void LoadFromDirectory(string path
                                         , LoadAction action = null);

    /// <summary>
    /// Replaces any characters not allowed for a file name in a string with 
    /// an underscore.
    /// </summary>
    /// <param name="value">The string in which to replace the non allowed 
    /// characters.</param>
    /// <returns>A new string in which all non allowed characters have been 
    /// replaced.</returns>
    /// <created>2015-02-07</created><changed>2021-12-12</changed>
    protected static string RemoveInvalidTitleChars(string value)
    {
      return Regex.Replace(value, string.Format("[{0}]"
           , Regex.Escape(new string(Path.GetInvalidFileNameChars()))), "_");
    }

    /// <summary>
    /// Resets the storable properties to the default values.
    /// </summary>
    /// <created>2015-02-08</created><modified>2015-02-08</modified>
    public virtual void Reset()
    {
      this.Title = "";
      this.Executable = true;
    }

    /// <summary>
    /// An abstract method to save the item content into a file or directory.
    /// </summary>
    /// <param name="path">A file system path to a file or directory</param>
    /// <created>2015-02-07</created><modified>2021-12-11</modified>
    public abstract void SaveToDirectory(string path);

  }
}
