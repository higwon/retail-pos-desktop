using RetailPOS.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRetailPosApi();

var app = builder.Build();
app.UseRetailPosApi();

app.Run();

public partial class Program;
