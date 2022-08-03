namespace RMQ.Client.Abstractions.Producing;

public delegate Task ProducerDelegate(ProducerContext context);

public delegate Task ProducerDelegate<TNativeProperties>(ProducerContext<TNativeProperties> context);