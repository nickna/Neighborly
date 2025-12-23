namespace Neighborly;

/// <summary>
/// Exception thrown when a transaction operation fails.
/// </summary>
public class TransactionException : Exception
{
    /// <summary>
    /// The ID of the transaction that failed.
    /// </summary>
    public Guid TransactionId { get; }

    public TransactionException(string message, Guid transactionId)
        : base(message)
    {
        TransactionId = transactionId;
    }

    public TransactionException(string message, Guid transactionId, Exception innerException)
        : base(message, innerException)
    {
        TransactionId = transactionId;
    }
}
