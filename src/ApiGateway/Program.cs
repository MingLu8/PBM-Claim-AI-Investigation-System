using ApiGateway;
using ApiGateway.Endpoints;
using ApiGateway.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddGeminiChatClient(builder.Configuration);
builder.Services.AddGatewayInfrastructure(builder.Configuration);
builder.Services.AddHostedService<InvestigationWorker>();
builder.Services.AddEndpoints(typeof(Program).Assembly);
var app = builder.Build();
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapEndpoints();

await app.RunAsync();