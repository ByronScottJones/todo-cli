using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;
using Xunit;

namespace Todo.CLI.Tests.Dependencies;

public class GraphClientTests
{
    // Repository tests mock Graph, so a Graph/Kiota version mismatch only surfaces at runtime
    // (MissingMethodException in GraphClientFactory). Constructing the real client catches it.
    [Fact]
    public void GraphServiceClient_Constructs_WithResolvedKiotaPackages()
    {
        var client = new GraphServiceClient(new AnonymousAuthenticationProvider());

        Assert.NotNull(client);
    }
}
