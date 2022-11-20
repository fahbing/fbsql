using System;
using System.Text.RegularExpressions;


namespace Fahbing.Sql
{
  /// <summary>
  /// Comparison operators.
  /// </summary>
  public enum Comparison
  {
    equal,
    greater,
    greaterOrEqual,
    smaller,
    smallerOrEqual,
    unequal
  }

  /// <summary>
  /// Represents a condition for comparison to a SQL Server compatibility 
  /// level.
  /// </summary>
  public class SqlCompLevelCondition
  {
    private const string ResErrCompLevelCond = "Invalid conditional expression for compatibility level.";

    /// <summary>
    /// The comparison operator.
    /// </summary>
    public Comparison Comparison { get; set; } = Comparison.equal;

    /// <summary>
    /// The compatibility level comparison value.
    /// </summary>
    public int Level { get; set; } = 0;


    /// <summary>
    /// Creates a new Instance of the CompLevelCondition class.
    /// </summary>
    /// <param name="condition">The compatibility level condition.</param>
    /// <exception cref="ApplicationException"></exception>
    public SqlCompLevelCondition(string condition)
    {
      if (string.IsNullOrEmpty(condition))
        throw new ApplicationException(ResErrCompLevelCond);

      MatchCollection matches = Regex.Matches(condition
        , "^([=<>]{1}|[<>!]{1}[=]?|[<]{1}[>]{1}) *(\\d+)$");

      if (matches.Count == 1 && matches[0].Groups.Count == 3)
      {
        Comparison = matches[0].Groups[1].ToString() switch
        {
          "=" => Comparison.equal,
          ">" => Comparison.greater,
          ">=" => Comparison.greaterOrEqual,
          "<" => Comparison.smaller,
          "<=" => Comparison.smallerOrEqual,
          "<>" or "!=" => Comparison.unequal,
          _ => throw new ApplicationException(ResErrCompLevelCond),
        };
        Level = Convert.ToInt32(matches[0].Groups[2].Value);
      }
    }

    /// <summary>
    /// Compares a value to the compatibility level condition.
    /// </summary>
    /// <param name="compatibiltyLevel">The value to be compared.</param>
    /// <returns>Returns <see langword="true"/> if the condition is true, <see 
    /// langword="false"/> otherwise.
    /// </returns>
    public bool Compare(int compatibiltyLevel) 
    {
      return Comparison switch
      {
        Comparison.equal => compatibiltyLevel == Level,
        Comparison.greater => compatibiltyLevel > Level,
        Comparison.greaterOrEqual => compatibiltyLevel >= Level,
        Comparison.smaller => compatibiltyLevel < Level,
        Comparison.smallerOrEqual => compatibiltyLevel <= Level,
        Comparison.unequal => compatibiltyLevel != Level,
        _ => false,
      };
    }

    /// <summary>
    /// Returns the compatibility level condition as string.
    /// </summary>
    /// <returns>The condition definition as string.</returns>
    public override string ToString() 
    {
      return Comparison switch
      {
        Comparison.greater => ">",
        Comparison.greaterOrEqual => ">=",
        Comparison.smaller => "<",
        Comparison.smallerOrEqual => "<",
        Comparison.unequal => "<>",
        _ => "=",
      } + Level.ToString();
    }
  }

}
