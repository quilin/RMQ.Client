using RabbitMQ.Client.Events;

namespace RMQ.Client.Abstractions.Consuming;

public class ConsumerContext<TMessage> : ConsumerContext
{
    internal ConsumerContext(BasicDeliverEventArgs deliverEventArgs, IServiceProvider serviceProvider)
        : base(deliverEventArgs, serviceProvider)
    {
    }

    internal static ConsumerContext<TMessage>From(ConsumerContext context) =>
        context switch
        {
            ConsumerContext<TMessage> genericContext => genericContext,
            _ => new ConsumerContext<TMessage>(context.NativeDeliverEvent, context.ServiceProvider)
            {
                StoredValues = context.StoredValues
            }
        };

    /// <summary>
    /// Decoded incoming message
    /// </summary>
    public TMessage? Message { get; set; }
}

/// <summary>
/// Incoming message pipeline context
/// </summary>
public abstract class ConsumerContext
{
    /// <summary>
    /// Native RabbitMQ library incoming message event
    /// </summary>
    public BasicDeliverEventArgs NativeDeliverEvent { get; }

    /// <summary>
    /// Service provider
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Values to share between the pipeline steps
    /// </summary>
    public IDictionary<string, object> StoredValues { get; internal init; } = new Dictionary<string, object>();

    protected ConsumerContext(
        BasicDeliverEventArgs deliverEventArgs,
        IServiceProvider serviceProvider)
    {
        NativeDeliverEvent = deliverEventArgs;
        ServiceProvider = serviceProvider;
    }
}