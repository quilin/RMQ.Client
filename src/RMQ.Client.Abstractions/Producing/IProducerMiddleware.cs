namespace RMQ.Client.Abstractions.Producing;

/// <summary>
/// Universal producer pipeline middleware
/// ()
/// </summary>
public interface IProducerMiddleware
{
    /// <summary>
    /// Produce handling method
    /// </summary>
    /// <param name="context">The <see cref="ProducerContext"/> for current produce</param>
    /// <param name="next">The delegate representing the remaining middleware in the produce pipeline</param>
    /// <returns>A <see cref="Task"/> that represents the execution of this middleware</returns>
    Task InvokeAsync(ProducerContext context, ProducerDelegate next);
}

public interface IProducerMiddleware<TNativeProperties>
{
    /// <summary>
    /// Produce handling method
    /// </summary>
    /// <param name="context">The <see cref="ProducerContext"/> for current produce</param>
    /// <param name="next">The delegate representing the remaining middleware in the produce pipeline</param>
    /// <returns>A <see cref="Task"/> that represents the execution of this middleware</returns>
    Task InvokeAsync(ProducerContext<TNativeProperties> context, ProducerDelegate<TNativeProperties> next);
}