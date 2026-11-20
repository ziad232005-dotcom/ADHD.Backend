using ADHD.Backend.Auth;
using ADHD.Backend.Link;
using ADHD.Backend.Shadows;
using ADHD.Backend.Sms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// SMS verification (the phone that sends the messages is configured in
// User Secrets, see SMS_SETUP.md).
builder.Services.Configure<SmsGatewayOptions>(
    builder.Configuration.GetSection(SmsGatewayOptions.SectionName));

builder.Services.AddHttpClient<ISmsSender, AndroidSmsGatewaySender>(
    client => client.Timeout = TimeSpan.FromSeconds(10));

// One service keeps the verification codes of emails and phone numbers.
builder.Services.AddSingleton<OtpService>();
builder.Services.AddOtpRateLimits();

// Short codes that hand a child's data from the parent's phone to theirs.
builder.Services.AddSingleton<LinkCodeService>();

// Shadow Teachers apply here, the admin approves, parents read the list.
builder.Services.AddSingleton<ShadowStore>();

var app = builder.Build();

// Local testing runs over plain http (the Android emulator cannot trust
// the development certificate). Anywhere else, only https is accepted.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    app.MapOpenApi();
}

app.UseRateLimiter();
app.UseStaticFiles();

app.MapGet("/", () => Results.Ok(new
{
    message = "MAZID Backend is running"
}));

app.MapEmailAuthEndpoints();
app.MapSmsAuthEndpoints();
app.MapLinkEndpoints();
app.MapShadowEndpoints();
app.MapGet("/admin", () => Results.Redirect("/admin.html"));

app.Run();
