using System;
using System.Collections;
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
  /// Represents a collection of <see cref="SqlTreeItem"/> instances that are 
  /// of either class <see cref="SqlTreeStep"/> or class <see 
  /// cref="SqlTreeBatch"/>. <see cref="SqlTreeBatch"/> is a descendant from 
  /// <see cref="SqlTreeItem"/>.
  /// </summary>
  /// <created>2006-07-05</created><modified>2021-12-15/modified>
  public class SqlTreeBatch : SqlTreeItem, IEnumerable
  {
    /// <summary>
    /// An indexer for a <see cref="SqlTreeBatch"/> instance to access the 
    /// <see cref="Items"/>.
    /// </summary>
    /// <param name="index">The item index.</param>
    /// <returns>A <see cref="SqlTreeItem"/> instance.</returns>
    public SqlTreeItem this[int index] => Items[index];

    /// <summary>
    /// Return the count of <see cref="Items"/>.
    /// </summary>
    public int Count => Items.Count;

    protected List<SqlTreeItem> Items { get; private set; }

    /// <summary>
    /// Indicates whether the batch node is expanded or collapsed.
    /// </summary>
    public bool Expanded { get; set; }


    /// <summary>
    /// Creates a new <see cref="SqlTreeBatch"/> instance.
    /// </summary>
    /// <param name="parent">The parent <see cref="SqlTreeBatch"/> object.
    /// </param>
    /// <created>2006-07-05</created><modified>2021-12-15/modified>
    public SqlTreeBatch(SqlTreeBatch parent)
      : base(SqlTreeItemType.batch, parent)
    {
      Items = new List<SqlTreeItem>();

      if (Parent != null)
      {
        parent.Add(this);
      }
    }

    /// <summary>
    /// Creates a new <see cref="SqlTreeBatch"/> instance.
    /// </summary>
    /// <param name="batch">The definition of the batch as an <see 
    /// cref="XElement"/> instance.</param>
    /// <param name="parent">The parent <see cref="SqlTreeBatch"/> object.
    /// </param>
    /// <created>2006-07-05</created><modified>2021-12-15/modified>
    public SqlTreeBatch(XElement batch,
                        SqlTreeBatch parent)
      : this(parent) => LoadFromXElement(batch);


    /// <summary>
    /// Appends a <see cref="SqlTreeItem"/> instance to the batch.
    /// </summary>
    /// <param name="item">The <see cref="SqlTreeStep"/> or <see 
    /// cref="SqlTreeBatch"/> instance to add to the batch.</param>
    /// <created>2006-07-05</created><modified>2022-05-26/modified>
    public void Add(SqlTreeItem item)
    {
      if (item != null)
      {
        if (item.Parent != null)
        {
          item.Parent.Remove(item);
        }

        Items.Add(item);

        item.Parent = this;
      }
    }

    /// <summary>
    /// Removes all items.
    /// </summary>
    /// <created>2006-07-05</created><modified>2021-12-15/modified>
    public void Clear()
    {
      if (Items != null)
      {
        Items.Clear();
      }
    }

    /// <summary>
    /// Create a clone of the <see cref="SqlTreeBatch"/> instance.
    /// </summary>
    /// <returns>A new cloned <see cref="SqlTreeBatch"/> instance.</returns>
    /// <created>2015-07-11</created><modified>2022-05-26/modified>
    public override object Clone()
    {
      return new SqlTreeBatch(GetXElement(), null);
    }

    /// <summary>
    /// Calls a delegate function for each executable subobject.
    /// </summary>
    /// <param name="action">The delegate function to call.</param>
    /// <created>2017-07-13</created><modified>2021-12-15/modified>
    public void ForEachExecutableItem(Action<SqlTreeItem> action)
    {
      foreach (SqlTreeItem item in this)
      {
        if (item.Executable)
        {
          action(item);

          if (item.ItemType == SqlTreeItemType.batch)
          {
            ((SqlTreeBatch)item).ForEachExecutableItem(action);
          }
        }
      }
    }

    /// <summary>
    /// Calls a delegate function for each SQL command in all executable 
    /// subobjects.
    /// </summary>
    /// <param name="action">The delegate function to call.</param>
    /// <param name="parser">An optional StringParser instance that passes 
    /// down. It is used to determine the SQL commands in the steps.</param>
    /// <created>2017-07-13</created><modified>2021-03-13</modified>
    public void ForEachExecutableCommand(
                             Action<SqlTreeItem, SqlScriptCommand, int> action,
                             StringParser parser = null)
    {
      if (parser == null)
      {
        parser = new StringParser();

        parser.SetTSQLOptions();
      }

      foreach (SqlTreeItem item in this)
      {
        if (item.Executable)
        {
          if (item.ItemType == SqlTreeItemType.batch)
          {
            action(item, null, 0);
            ((SqlTreeBatch)item).ForEachExecutableCommand(action, parser);
          }
          else
          {
            List<SqlScriptCommand> commands = ((SqlTreeStep)item).GetCommands(parser);
            int index = 0;

            foreach (SqlScriptCommand command in commands)
            {
              action(item, command, index++);
            }
          }
        }
      }
    }

    /// <summary>
    /// Implementation method of the <see cref="INumerator"/> interface.
    /// </summary>
    /// <returns>an enumerator that iterates through the <see cref="Items"/> 
    /// list.</returns>
    /// <created>2015-02-07</created><modified>2021-12-15/modified>
    public IEnumerator GetEnumerator()
    {
      return Items.GetEnumerator();
    }

    /// <summary>
    /// Searchs an item and returns the 0-based index.
    /// </summary>
    /// <param name="item">The <see cref="SqlTreeItem"/> instance to search.
    /// </param>
    /// <returns>The 0-based index of the item if it found, otherwise -1.
    /// </returns>
    /// <created>2015-07-12</created><modified>2021-12-15/modified>
    public int IndexOf(SqlTreeItem item)
    {
      return Items.IndexOf(item);
    }

    /// <summary>
    /// Inserts a <see cref="SqlTreeItem"/> instance into the batch at the 
    /// specified index.
    /// </summary>
    /// <param name="index">The 0-based index at which item should be inserted.
    /// </param>
    /// <param name="item">The <see cref="SqlTreeItem"/> instance to insert.
    /// </param>
    /// <created>2006-07-05</created><modified>2021-12-15/modified>
    public void Insert(int index,
                       SqlTreeItem item)
    {
      if (item != null)
      {
        if (item.Parent != null)
        {
          item.Parent.Remove(item);
        }

        Items.Insert(index, item);

        item.Parent = this;
      }
    }

    /// <summary>
    /// Inserts a <see cref="SqlTreeItem"/> instance after another item.
    /// </summary>
    /// <param name="targetItem">The item object after which the new object 
    /// is to be inserted.</param>
    /// <param name="item">The <see cref="SqlTreeItem"/> instance to insert.
    /// </param>
    /// <created>2015-07-11</created><modified>2021-12-15/modified>
    public void InsertAfter(SqlTreeItem targetItem,
                            SqlTreeItem item)
    {
      int index = IndexOf(targetItem);

      if (index >= 0)
      {
        Insert(++index, item);
      }
    }

    /// <summary>
    /// Inserts a <see cref="SqlTreeItem"/> instance before another item.
    /// </summary>
    /// <param name="targetItem">The item object before which the new object 
    /// is to be inserted.</param>
    /// <param name="item">The <see cref="SqlTreeItem"/> instance to insert.
    /// </param>
    /// <created>2015-07-11</created><modified>2021-12-15/modified>
    public void InsertBefore(SqlTreeItem targetItem,
                             SqlTreeItem item)
    {
      int index = IndexOf(targetItem);

      if (index >= 0)
      {
        Insert(index, item);
      }
    }

    /// <summary>
    /// Gets the defintion of this item as an <see cref="XElement"/>.
    /// </summary>
    /// <returns></returns>
    /// <created>2015-02-07</created><modified>2021-12-29</modified>
    public override XElement GetXElement()
    {
      XElement xBatch = new("batch", new XAttribute("name", Title)
                          , new XAttribute("executable"
                                         , Executable.ToString().ToLower())
                          , new XAttribute("expanded"
                                         , Expanded.ToString().ToLower()));

      foreach (SqlTreeItem item in this)
        xBatch.Add(item.GetXElement());

      return xBatch;
    }

    /// <summary>
    /// Loads a <see cref="SqlTreeBatch"/> from an <see cref="XElement"/> 
    /// instance.
    /// </summary>
    /// <param name="batch">An <see cref="XElement"/> instance that contains 
    /// the definition of the <see cref="SqlTreeBatch"/>.</param>
    /// <created>2015-02-07</created><modified>2022-05-26</modified>
    protected void LoadFromXElement(XElement batch)
    {
      base.Reset();

      Title = GetStringFromXAttribute(batch.Attribute("name"));
      Executable = GetBooleanFromXAttribute(batch.Attribute("executable"));
      Expanded = GetBooleanFromXAttribute(batch.Attribute("expanded"));

      foreach (XElement item in batch.Elements())
      {
        if (item.Name == "batch")
        {
          new SqlTreeBatch(item, this);
        }
        else if (item.Name == "step")
        {
          new SqlTreeStep(item, this);
        }
      }
    }

    /// <summary>
    /// Loads the batch content from a directory.
    /// </summary>
    /// <param name="path">A file system path to the directory.</param>
    /// <param name="action">A <see cref="LoadAction"/> delegate function, 
    /// e.g. for progress indicators.</param>
    /// <param name="debug">Specifies whether debug information should be 
    /// stored.</param>
    /// <created>2015-02-07</created><modified>2022-08-19</modified>
    public override void LoadFromDirectory(string path
                                         , LoadAction action = null
                                         , bool debug = false)
    {
      path = IncludeTrailingPathDelimiter(path);

      action?.Invoke(SqlTreeItemType.batch, path);

      if (!File.Exists(Path.Combine(path, "__batch.xml")))
      {
        int index = ExcludeTrailingPathDelimiter(path)
                   .LastIndexOf(Path.DirectorySeparatorChar);
        Expanded = true;
        Title = path.Substring(index + 1, path.Length - (index + 2));

        if (Regex.IsMatch(Title, "^\\d{4}-"))
          Title = Title.Substring(5);
      }
      else
        LoadFromXElement(XDocument.Load(Path.Combine(path, "__batch.xml"))
                        .Element("batch"));

      string[] directories = Directory.GetDirectories(path);
      string[] files = Directory.GetFiles(path, "*.sql");
      string[] elements = new string[directories.Length + files.Length];

      directories.CopyTo(elements, 0);
      files.CopyTo(elements, directories.Length);

      Array.Sort(elements);

      foreach (string name in elements)
      {
        if (Directory.Exists(name))
        {
          if (!(new DirectoryInfo(name)).Name.StartsWith("."))
          {
            new SqlTreeBatch(this).LoadFromDirectory(name, action, debug);
          }
        }
        else
        {
          new SqlTreeStep(this).LoadFromDirectory(name, action, debug);
        }
      }
    }

    /// <summary>
    /// Removes a <see cref="SqlTreeItem"/> instance from the batch.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns><see langword="true"/> if the item is successfully removed or 
    /// <see langword="false"/> if the item was not found.</returns>
    /// <created>2006-07-05</created><modified>2021-12-29</modified>
    public bool Remove(SqlTreeItem item)
    {
      if (item != null && Items.Remove(item))
      {
        item.Parent = null;

        return true;
      }

      return false;
    }

    /// <summary>
    /// Removes all entries from <see cref="items"/> and resets the storable 
    /// properties to the default values.
    /// </summary>
    /// <created>2015-02-08</created><modified>2021-12-15</modified>
    public override void Reset()
    {
      Expanded = true;

      base.Reset();
      Clear();
    }

    /// <summary>
    /// Saves the <see cref="SqlTreeBatch"/> instance as a directory. 
    /// According to their type, the objects in items are created as files or 
    /// subdirectories.
    /// </summary>
    /// <param name="path">A file system path to the destination directory.
    /// </param>
    /// <created>2015-02-07</created><modified>2021-12-15</modified>
    public override void SaveToDirectory(string path)
    {
      int index = 0;
      path = IncludeTrailingPathDelimiter(path);

      Directory.CreateDirectory(path);

      new XElement("batch",
                   new XAttribute("name", string.IsNullOrWhiteSpace(Title)
                                ? "" : Title),
                   new XAttribute("executable"
                                , Executable.ToString().ToLower()),
                   new XAttribute("expanded", Expanded.ToString().ToLower())
                  ).Save(Path.Combine(path, "__batch.xml"));

      foreach (SqlTreeItem item in this)
      {
        index++;

        if (item.ItemType == SqlTreeItemType.batch)
        {
          item.SaveToDirectory(path + index.ToString().PadLeft(3, '0') + "0"
                             + "-" + RemoveInvalidTitleChars(item.Title)
                             + Path.DirectorySeparatorChar);
        }
        else if (item.ItemType == SqlTreeItemType.step)
        {
          item.SaveToDirectory(path + index.ToString().PadLeft(3, '0') + "0"
                             + "-" + RemoveInvalidTitleChars(item.Title)
                             + ".sql");
        }
      }
    }
  }

}
