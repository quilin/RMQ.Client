namespace RMQ.Client.Abstractions.Producing;

/// <summary>
/// Producer pipeline context
/// </summary>
public abstract class ProducerContext
{
    public string RoutingKey { get; }
    public object Message { get; }
    public IServiceProvider ServiceProvider { get; }

    public IDictionary<string, object> StoredValues { get; internal init; } = new Dictionary<string, object>();
    
    public byte[]? Body { get; set; }

    protected ProducerContext(
        string routingKey,
        object message,
        IServiceProvider serviceProvider)
    {
        RoutingKey = routingKey;
        Message = message;
        ServiceProvider = serviceProvider;
    }
}

public class ProducerContext<TNativeProperties> : ProducerContext
{
    internal ProducerContext(string routingKey, object message, IServiceProvider serviceProvider) : base(routingKey, message, serviceProvider)
    {
    }

    internal static ProducerContext<TNativeProperties> From(ProducerContext context) => context switch
    {
        ProducerContext<TNativeProperties> genericContext => genericContext,
        _ => new ProducerContext<TNativeProperties>(context.RoutingKey, context.Message, context.ServiceProvider)
        {
            StoredValues = context.StoredValues
        }
    };

    public TNativeProperties? NativeProperties { get; set; }
}