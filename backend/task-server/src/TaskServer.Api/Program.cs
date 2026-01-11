using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskServer.Api.Hubs;
using TaskServer.Core.Interfaces;
using TaskServer.Infrastructure.Authentication;
using TaskServer.Infrastructure.Authorization;
using TaskServer.Infrastructure.Executors;
using TaskServer.Infrastructure.Extensions;
using TaskServer.Infrastructure.Services;
using TaskServer.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// Configure Firebase Authentication options
var firebaseOptions = builder.Configuration
    .GetSection(FirebaseAuthenticationOptions.SectionName)
    .Get<FirebaseAuthenticationOptions>() ?? new FirebaseAuthenticationOptions();

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Add controllers with JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("SignalR:KeepAliveIntervalSeconds", 15));
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("SignalR:ClientTimeoutSeconds", 30));
});

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Task Server API",
        Version = "v1",
        Description = "Task management and execution API with real-time progress tracking"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add JWT Authentication (Firebase)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = firebaseOptions.ValidIssuer;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = firebaseOptions.ValidIssuer,
            ValidateAudience = true,
            ValidAudience = firebaseOptions.ValidAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Add Authorization
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("TaskOwner", policy =>
        policy.Requirements.Add(new TaskOwnerRequirement()));
});

builder.Services.AddSingleton<IAuthorizationHandler, TaskOwnerAuthorizationHandler>();

// Add Task Server services
builder.Services.AddTaskServer(options =>
{
    options.MaxConcurrentTasks = builder.Configuration.GetValue("TaskServer:MaxConcurrentTasks", Environment.ProcessorCount);
    options.DefaultTaskTimeoutMinutes = builder.Configuration.GetValue("TaskServer:DefaultTaskTimeoutMinutes", 60);
    options.TaskQueuePollingIntervalMs = builder.Configuration.GetValue("TaskServer:TaskQueuePollingIntervalMs", 100);
});

builder.Services.AddInMemoryTaskStorage();

// Add Task History services
builder.Services.AddSingleton<InMemoryTaskHistoryRepository>();
builder.Services.AddScoped<ITaskHistoryService, TaskHistoryService>();

// Add HttpClient factory for microservice calls
builder.Services.AddHttpClient("TaskExecutor");
builder.Services.AddSingleton<ITaskHttpClientFactory, TaskHttpClientFactory>();

// Register task executors
builder.Services.AddTaskExecutor<DemoTaskExecutor>();
builder.Services.AddTaskExecutor<SimpleHierarchicalExecutor>();
builder.Services.AddTaskExecutor<HierarchicalParentExecutor>();

// Load plugin executors from plugins folder
var pluginsPath = Path.Combine(AppContext.BaseDirectory, "plugins");
builder.Services.AddTaskExecutorPlugins(pluginsPath);

// Register NotificationService with the concrete TaskHub type
builder.Services.AddScoped<INotificationService, NotificationService<TaskHub>>();

// Add Health Checks
builder.Services.AddHealthChecks();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    options.AddPolicy("SignalR", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Initialize default task groups
using (var scope = app.Services.CreateScope())
{
    var groupService = scope.ServiceProvider.GetRequiredService<ITaskGroupService>();
    await groupService.EnsureDefaultGroupsExistAsync();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Task Server API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// Map health check endpoints
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// Map controllers
app.MapControllers();

// Map SignalR hub with CORS
app.MapHub<TaskHub>("/hubs/tasks").RequireCors("SignalR");

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
