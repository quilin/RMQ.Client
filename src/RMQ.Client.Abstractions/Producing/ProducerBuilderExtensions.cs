namespace RMQ.Client.Abstractions.Producing;

/// <summary>
/// Extensions for typed middleware configuration
/// </summary>
public static class ProducerBuilderExtensions
{
    /// <summary>
    /// Add middleware of given type to the pipeline
    /// </summary>
    /// <param name="builder">Producer builder</param>
    /// <param name="args">Middleware constructor arguments</param>
    /// <typeparam name="TMiddleware">Middleware type</typeparam>
    /// <returns>Builder itself for chaining</returns>
    public static IProducerBuilder WithMiddleware<TMiddleware>(
        this IProducerBuilder builder, params object[] args) =>
        builder.WithMiddleware(typeof(TMiddleware), args);
}