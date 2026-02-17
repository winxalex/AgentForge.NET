namespace Chat2Report.Agents.Tools 
{
    /// <summary>
    /// Represents a function that modifies a numeric value
    /// </summary>
    /// <param name="value">The input value to modify</param>
    /// <returns>The modified value</returns>
    public delegate int ModifyFunction(int value);

    /// <summary>
    /// Represents a predicate that determines if processing should terminate
    /// </summary>
    /// <param name="value">The value to evaluate</param>
    /// <returns>True if processing should terminate, false otherwise</returns> 
    public delegate bool TerminationFunction(int value);
}