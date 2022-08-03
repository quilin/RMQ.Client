using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Producing;
using RMQ.Client.Connection;
using RMQ.Client.Connection.Adapters;

namespace RMQ.Client.Producing;

internal class Producer : IProducer
{
    private readonly IChannelPool channelPool;
    private readonly IServiceProvider serviceProvider;
    private readonly RabbitProducerParameters parameters;
    private readonly ProducerDelegate<IBasicProperties> pipeline;

    private Lazy<IChannelAdapter> channelAccessor;

    public Producer(
        IChannelPool channelPool,
        IServiceProvider serviceProvider,
        RabbitProducerParameters parameters,
        IEnumerable<Func<ProducerDelegate<IBasicProperties>, ProducerDelegate<IBasicProperties>>> middlewares)
    {
        this.channelPool = channelPool;
        this.serviceProvider = serviceProvider;
        this.parameters = parameters;

        pipeline = middlewares.Reverse().Aggregate((ProducerDelegate<IBasicProperties>)SendMessage,
            (current, component) => component(current));
        channelAccessor = CreateChannelAccessor();
    }

    private Lazy<IChannelAdapter> CreateChannelAccessor() => new(() =>
    {
        var channel = channelPool.Get();
        channel.OnDisrupted += Restore!;
        Ensure.Produce(channel.Channel, parameters);
        return channel;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    private void Restore(object sender, ChannelDisruptedEventArgs e)
    {
        CloseCurrentChannel();
        channelAccessor = CreateChannelAccessor();
    }

    private void CloseCurrentChannel()
    {
        if (!channelAccessor.IsValueCreated)
        {
            return;
        }

        var channel = channelAccessor.Value;
        channel.OnDisrupted -= Restore!;
        channel.Dispose();
    }

    public async Task Send<TMessage>(string routingKey, TMessage message, CancellationToken cancellationToken)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        await using var scope = serviceProvider.CreateAsyncScope();

        var basicProperties = channelAccessor.Value.Channel.CreateBasicProperties();
        var context = new ProducerContext<IBasicProperties>(routingKey, message, scope.ServiceProvider)
        {
            NativeProperties = basicProperties
        };
        await pipeline.Invoke(context);
    }

    private Task SendMessage(ProducerContext<IBasicProperties> context)
    {
        var channelAdapter = channelAccessor.Value;
        var channel = channelAdapter.Channel;

        var waitForConfirms = parameters.PublishingTimeout.HasValue;
        if (waitForConfirms)
        {
            channel.ConfirmSelect();
        }

        channel.BasicPublish(parameters.ExchangeName, context.RoutingKey, true, context.NativeProperties, context.Body);

        if (waitForConfirms)
        {
            channel.WaitForConfirmsOrDie(parameters.PublishingTimeout!.Value);
        }

        return Task.CompletedTask;
    }

    public void Dispose() => CloseCurrentChannel();
}