using PocketStoryPoker.Hubs;
using PocketStoryPoker.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ILoggingService, LoggingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapHub<PokerHub>("/pokerhub");  // Only map the PokerHub
app.MapFallbackToPage("/_Host");

app.Run();
