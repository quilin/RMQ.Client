using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Producing;
using RMQ.Client.Connection;

namespace RMQ.Client.Producing;

internal class ProducerBuilder : IProducerBuilder
{
    private readonly IList<object> middlewares = new List<object>();

    public ProducerBuilder(
        IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }
    
    public IProducerBuilder With(Func<ProducerDelegate, ProducerDelegate> middleware)
    {
        middlewares.Add(middleware);
        return this;
    }

    public IProducerBuilder WithMiddleware(Type middlewareType, params object[] args)
    {
        middlewares.Add((middlewareType, args));
        return this;
    }

    public IProducerBuilder Flush()
    {
        middlewares.Clear();
        return this;
    }

    public IProducer BuildRabbit(RabbitProducerParameters parameters)
    {
        var components = middlewares.Select(ToPipelineStep<IBasicProperties>);
        var channelPool = ServiceProvider.GetRequiredService<IProducerChannelPool>();
        return new Producer(channelPool, ServiceProvider, parameters, components);
    }

    private Func<ProducerDelegate<TNativeProperties>, ProducerDelegate<TNativeProperties>> ToPipelineStep<TNativeProperties>(object description) => description switch
    {
        Func<ProducerDelegate, ProducerDelegate> clientAgnosticMiddleware => next => context =>
        {
            var clientAgnosticDelegate = (ProducerDelegate) (clientAgnosticContext =>
                next(ProducerContext<TNativeProperties>.From(clientAgnosticContext)));
            var resultingDelegate = clientAgnosticMiddleware(clientAgnosticDelegate);
            return resultingDelegate(context);
        },
        (Type type, object[]) when typeof(IProducerMiddleware).GetTypeInfo()
            .IsAssignableFrom(type.GetTypeInfo()) => next => context =>
        {
            var middleware = (IProducerMiddleware<TNativeProperties>)context.ServiceProvider.GetRequiredService(type);
            return middleware.InvokeAsync(context, next);
        },
        (Type type, object[] args) => next =>
        {
            var methodInfo = type.GetMethod(
                nameof(IProducerMiddleware.InvokeAsync), BindingFlags.Instance | BindingFlags.Public);

            if (methodInfo is null)
            {
                throw new InvalidOperationException($"Middleware has to have a method {nameof(IProducerMiddleware.InvokeAsync)}");
            }

            var parameters = methodInfo.GetParameters();
            if (parameters.Length == 0 || parameters[0].ParameterType != typeof(ProducerContext))
            {
                throw new InvalidOperationException($"Middleware method {nameof(IProducerMiddleware.InvokeAsync)} has to have first parameter of type {nameof(ProducerContext)}");
            }

            var constructorArgs = new object[args.Length + 1];
            constructorArgs[0] = next;
            Array.Copy(args, 0, constructorArgs, 1, args.Length);
            var instance = ActivatorUtilities.CreateInstance(ServiceProvider, type, constructorArgs);

            if (parameters.Length == 1)
            {
                var producerDelegate = methodInfo.CreateDelegate(typeof(ProducerDelegate<TNativeProperties>), instance);
                return (ProducerDelegate<TNativeProperties>)producerDelegate;
            }

            var factory = Compile<object>(methodInfo, parameters);
            return context =>
            {
                var services = context.ServiceProvider;
                return factory(instance, context, services);
            };
        },
        _ => throw new NotSupportedException()
    };

    private static Func<TMiddleware, ProducerContext, IServiceProvider, Task> Compile<TMiddleware>(
        MethodInfo methodInfo, IReadOnlyList<ParameterInfo> parameters)
    {
        var middleware = typeof(TMiddleware);

        var producerContextArg = Expression.Parameter(typeof(ProducerContext), "producerContext");
        var providerArg = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        var instanceArg = Expression.Parameter(middleware, "middleware");

        var methodArguments = new Expression[parameters.Count];
        methodArguments[0] = producerContextArg;
        for (var i = 1; i < parameters.Count; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType.IsByRef)
            {
                throw new NotSupportedException();
            }

            var parameterTypeExpression = new Expression[]
            {
                providerArg,
                Expression.Constant(parameters, typeof(Type))
            };

            var getServiceCall = Expression.Call(GetServiceInfo, parameterTypeExpression);
            methodArguments[i] = Expression.Convert(getServiceCall, parameterType);
        }

        Expression middlewareInstanceArg = instanceArg;
        if (methodInfo.DeclaringType is not null && methodInfo.DeclaringType != typeof(TMiddleware))
        {
            middlewareInstanceArg = Expression.Convert(middlewareInstanceArg, methodInfo.DeclaringType);
        }

        var body = Expression.Call(middlewareInstanceArg, methodInfo, methodArguments);
        var lambda = Expression.Lambda<Func<TMiddleware, ProducerContext, IServiceProvider, Task>>(
            body, instanceArg, producerContextArg, providerArg);

        return lambda.Compile();
    }

    private static object GetService(IServiceProvider serviceProvider, Type type) =>
        serviceProvider.GetRequiredService(type);

    private static readonly MethodInfo GetServiceInfo = typeof(ProducerBuilderExtensions)
        .GetMethod(nameof(GetService), BindingFlags.NonPublic | BindingFlags.Static)!;

    public IServiceProvider ServiceProvider { get; }
}