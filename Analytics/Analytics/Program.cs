// Analytics service host. Endpoints, security, rate limiting and persistence are added
// by the US6 tasks (T223 onwards); this is the minimal compilable host created in Setup (T004).
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();

public partial class Program;