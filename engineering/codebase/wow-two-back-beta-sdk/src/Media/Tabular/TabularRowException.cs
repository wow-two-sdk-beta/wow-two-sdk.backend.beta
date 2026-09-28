namespace WoW.Two.Sdk.Backend.Beta.Media.Tabular;

/// <summary>Represents a document row whose cell does not convert to its property, with the row and column to fix.</summary>
public sealed class TabularRowException : Exception
{
    /// <summary>Creates the exception for a cell.</summary>
    /// <param name="row">The row number as the document shows it, header included.</param>
    /// <param name="column">The column header.</param>
    /// <param name="message">What is wrong with the cell.</param>
    public TabularRowException(int row, string column, string message)
        : base($"Row {row}, column '{column}': {message}")
    {
        Row = row;
        Column = column;
    }

    /// <summary>Creates the exception with a default message.</summary>
    public TabularRowException()
        : this(0, string.Empty, "The row is invalid.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public TabularRowException(string message)
        : base(message)
    {
        Column = string.Empty;
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public TabularRowException(string message, Exception innerException)
        : base(message, innerException)
    {
        Column = string.Empty;
    }

    /// <summary>Gets the row number as the document shows it.</summary>
    public int Row { get; }

    /// <summary>Gets the column header.</summary>
    public string Column { get; }
}
