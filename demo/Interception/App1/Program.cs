﻿using App1;
using Microsoft.Extensions.DependencyInjection;
using Dora.Interception;

var timeProvider = new ServiceCollection()
    .AddMemoryCache()
    .AddSingleton<ISystemTimeProvider, SystemTimeProvider>()
    .AddSingleton<SystemTimeProvider>()
    .BuildInterceptableServiceProvider(interception => interception.RegisterInterceptors(RegisterInterceptors))
    .GetRequiredService<SystemTimeProvider>();


Console.WriteLine("Utc time:");
for (int index = 0; index < 5; index++)
{
    Console.WriteLine($"{timeProvider.GetCurrentTime(DateTimeKind.Utc)}[{DateTime.UtcNow}]");
    await Task.Delay(1000);
}


Console.WriteLine("Local time:");
for (int index = 0; index < 5; index++)
{
    Console.WriteLine($"{timeProvider.GetCurrentTime(DateTimeKind.Local)}[{DateTime.Now}]");
    await Task.Delay(1000);
}

static void RegisterInterceptors(IInterceptorRegistry registry)
{
    var method = typeof(SystemTimeProvider).GetMethod("GetCurrentTime")!;
    registry.For<CachingInterceptor3<DateTimeKind, DateTime>>().ToMethod(1, typeof(SystemTimeProvider), method);
        
        //.ToMethod<SystemTimeProvider>(1, it => it.GetCurrentTime(default));
}