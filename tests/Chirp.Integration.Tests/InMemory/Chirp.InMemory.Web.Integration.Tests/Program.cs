using Chirp.Application.Common;
using Chirp.Application.Common.EventBusOptions;
using Chirp.Infrastructure;
using Chirp.InMemory.Web.Integration.Tests;
using Chirp.InMemory.Web.Integration.Tests.Handlers;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddChirp((InMemoryOptions options) =>
{
    options.EventBusType = EventBusType.InMemory;
    options.AddConsumer<NewMessageEventHandler>();
    options.AutoSubscribeConsumers = true;
});

builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();

host.UseChirp();
host.Run();