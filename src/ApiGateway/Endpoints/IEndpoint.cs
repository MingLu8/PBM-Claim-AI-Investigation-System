using Microsoft.AspNetCore.Routing;

namespace ApiGateway.Endpoints;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}