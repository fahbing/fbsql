
namespace Fahbing.Sql
{
  /// <summary>
  /// Contains debug informations for a <see cref="SqlScriptCommand"/>.
  /// </summary>
  public class DebugInfo
  {
    /// <summary>The source line number of a command in a step.</summary>
    public int Line { get; private set; }

    /// <summary>The source file name of a step in a folder script.</summary>
    public string SourcePath { get; private set; }


    /// <summary>
    /// Creates a new instance of the <see cref="DebugInfo"/> class.
    /// </summary>
    public DebugInfo(string sourcePath, int lineNumber = 0)
    {
      SourcePath = sourcePath;
      Line = lineNumber;
    }

  }
}
