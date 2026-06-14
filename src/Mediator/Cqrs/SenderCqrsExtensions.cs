namespace WoW.Two.Sdk.Backend.Beta.Mediator.Cqrs;

/// <summary>
/// CQRS-flavored dispatch facade over <see cref="ISender"/>. <c>SendAsync</c> overloads forward to
/// <see cref="ISender.Send{TResponse}(IRequest{TResponse}, System.Threading.CancellationToken)"/> /
/// <see cref="ISender.Send(IRequest, System.Threading.CancellationToken)"/> — providing the query/command
/// naming a product injects without forking the dispatch.
/// </summary>
public static class SenderCqrsExtensions
{
    /// <summary>Send a query and await its result.</summary>
    public static Task<TResult> SendAsync<TResult>(this ISender sender, IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return sender.Send(query, cancellationToken);
    }

    /// <summary>Send a command and await its result.</summary>
    public static Task<TResult> SendAsync<TResult>(this ISender sender, ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return sender.Send(command, cancellationToken);
    }

    /// <summary>Send a command that produces no response.</summary>
    public static Task SendAsync(this ISender sender, ICommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return sender.Send(command, cancellationToken);
    }
}
