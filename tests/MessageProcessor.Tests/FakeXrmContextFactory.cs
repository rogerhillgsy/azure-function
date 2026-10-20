using FakeXrmEasy;
using FakeXrmEasy.Abstractions;
using FakeXrmEasy.Abstractions.Enums;
using FakeXrmEasy.Middleware;
using FakeXrmEasy.Middleware.Crud;

namespace MessageProcessor.Tests;

internal static class FakeXrmContextFactory
{
    public static IXrmFakedContext Create() =>
        MiddlewareBuilder.New()
            .AddCrud()
            .UseCrud()
            .SetLicense(FakeXrmEasyLicense.NonCommercial)
            .Build();
}
