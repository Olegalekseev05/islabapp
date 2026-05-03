using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IsLabApp.Tests;

public sealed class VersionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public VersionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetVersion_ReturnsExpectedAppMetadata()
    {
        var response = await _client.GetAsync("/version");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<VersionResponse>();

        Assert.NotNull(payload);
        Assert.Equal("IsLabApp", payload.Name);
        Assert.Equal("1.0.0", payload.Version);
    }

    private sealed record VersionResponse(string Name, string Version);
}
