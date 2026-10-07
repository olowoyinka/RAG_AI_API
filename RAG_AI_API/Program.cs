using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Npgsql;
using RAG_AI_API;
using RAG_AI_API.Data;
using RAG_AI_API.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.Configure<RagOptions>(builder.Configuration.GetSection("Rag"));
builder.Services.Configure<HuggingFaceOptions>(builder.Configuration.GetSection("HuggingFace"));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Postgres"));
dataSourceBuilder.UseVector();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddSingleton(dataSource);
builder.Services.AddDbContextPool<ApplicationDbContext>(options => options.UseNpgsql(dataSource, o => o.UseVector()));

builder.Services.AddScoped<IChunkingService, ChunkingService>();
builder.Services.AddScoped<IContextBuilderService, ContextBuilderService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IGenerationService, GenerationService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IRerankerService, RerankerService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<ITextExtractionService, TextExtractionService>();
builder.Services.AddScoped<IVectorSearchService, VectorSearchService>();

builder.Services.AddHttpClient("huggingface", client =>
{
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("HuggingFace:TimeoutSeconds", 60));
});

builder.Services.AddHttpClient("ollama", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? string.Empty);

    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("Ollama:TimeoutSeconds", 120));
});

var awsConfig = builder.Configuration.GetSection("Storage");
var accessKey = awsConfig["AccessKey"];
var secretKey = awsConfig["SecretKey"];
var region = awsConfig["Region"];
var endpoint = awsConfig["Endpoint"];

var config = new AmazonS3Config
{
    RegionEndpoint = RegionEndpoint.EUCentral1,
    ForcePathStyle = true,
    ServiceURL = endpoint
};

var credentials = new BasicAWSCredentials(accessKey, secretKey);
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(credentials, config));

builder.Services.AddControllers();

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RAG API",
        Version = "v1",
        Description = "API documentation for RAG"
    });
});



var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "My RAG v1");

        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
