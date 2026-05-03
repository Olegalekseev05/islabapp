using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

var appSection = builder.Configuration.GetRequiredSection("App");
var appName = appSection["Name"] ?? throw new InvalidOperationException("App:Name is not configured.");
var appVersion = appSection["Version"] ?? throw new InvalidOperationException("App:Version is not configured.");
var mssqlConnectionString = builder.Configuration.GetConnectionString("Mssql")
    ?? throw new InvalidOperationException("ConnectionStrings:Mssql is not configured.");
var notes = new ConcurrentDictionary<Guid, Note>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    currentTime = DateTimeOffset.Now
}));

app.MapGet("/version", () => Results.Ok(new
{
    name = appName,
    version = appVersion
}));

app.MapGet("/db/ping", async (CancellationToken cancellationToken) =>
{
    try
    {
        await using var connection = new SqlConnection(mssqlConnectionString);
        await connection.OpenAsync(cancellationToken);

        return Results.Ok(new
        {
            status = "ok"
        });
    }
    catch (Exception exception)
    {
        return Results.Problem(
            title: "Database connection failed",
            detail: exception.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

var notesGroup = app.MapGroup("/api/notes");

notesGroup.MapPost("/", (CreateNoteRequest request) =>
{
    var validationErrors = ValidateCreateNoteRequest(request);
    if (validationErrors is not null)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var note = new Note(
        Guid.NewGuid(),
        request.Title.Trim(),
        request.Text.Trim(),
        DateTimeOffset.UtcNow);

    notes[note.Id] = note;

    return Results.Created($"/api/notes/{note.Id}", note);
})
.WithName("CreateNote")
.WithOpenApi();

notesGroup.MapGet("/", () => Results.Ok(notes.Values.OrderByDescending(note => note.CreatedAt)))
.WithName("GetNotes")
.WithOpenApi();

notesGroup.MapGet("/{id:guid}", (Guid id) =>
{
    return notes.TryGetValue(id, out var note)
        ? Results.Ok(note)
        : Results.NotFound(new { message = $"Note with id '{id}' was not found." });
})
.WithName("GetNoteById")
.WithOpenApi();

notesGroup.MapDelete("/{id:guid}", (Guid id) =>
{
    return notes.TryRemove(id, out _)
        ? Results.NoContent()
        : Results.NotFound(new { message = $"Note with id '{id}' was not found." });
})
.WithName("DeleteNote")
.WithOpenApi();

app.Run();

static Dictionary<string, string[]>? ValidateCreateNoteRequest(CreateNoteRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Title))
    {
        errors["title"] = ["Title is required."];
    }

    if (string.IsNullOrWhiteSpace(request.Text))
    {
        errors["text"] = ["Text is required."];
    }

    return errors.Count > 0 ? errors : null;
}

record Note(Guid Id, string Title, string Text, DateTimeOffset CreatedAt);

record CreateNoteRequest(string Title, string Text);

public partial class Program
{
}
