using System.Collections.Specialized;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles();


app.MapPost("/upload", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("Form content type is required.");

    var form = await request.ReadFormAsync();
    var title = form["title"].ToString();
    var file = form.Files["image"];

    // Validate file and title
    if (string.IsNullOrWhiteSpace(title) || file == null || file.Length == 0)
        return Results.BadRequest("Title and valid image file are required.");

    // Ensure file type is valid
    var allowedExtensions = new[] { "image/jpeg", "image/png", "image/gif" };
    if (!allowedExtensions.Contains(file.ContentType))
        return Results.BadRequest("Only jpeg, png, and gif files are allowed.");

    // Generate unique ID for the image
    var fileId = Guid.NewGuid().ToString();
    var filePath = Path.Combine("wwwroot", "uploads", $"{fileId}_{file.FileName}");

    // Create directory if it doesn't exist
    Directory.CreateDirectory("wwwroot/uploads");

    // Save the uploaded file
    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    // Save metadata to JSON file
    var imageData = new { Id = fileId, Title = title, FileName = filePath };
    var jsonData = Path.Combine("data", "images.json");
    Directory.CreateDirectory("data");

    List<dynamic> images = new List<dynamic>();
    if (File.Exists(jsonData))
    {
        var existingData = await File.ReadAllTextAsync(jsonData);
        images = JsonSerializer.Deserialize<List<dynamic>>(existingData) ?? new List<dynamic>();
    }

    images.Add(new Dictionary<string, string>
    {
        { "Id", fileId },
        { "Title", title },
        { "FileName", filePath }
    });

    await File.WriteAllTextAsync(jsonData, JsonSerializer.Serialize(images, new JsonSerializerOptions { WriteIndented = true }));

    // Redirect to view the uploaded image
    return Results.Redirect($"/picture/{fileId}");
});
app.MapGet("/picture/{id}", async (string id) =>
{
    var jsonData = Path.Combine("data", "images.json");
    if (!File.Exists(jsonData))
        return Results.NotFound("No images found.");

    var existingData = await File.ReadAllTextAsync(jsonData);
    var images = JsonSerializer.Deserialize<List<dynamic>>(existingData) ?? new List<dynamic>();

    var image = images.FirstOrDefault(i => i.Id == id);
    if (image == null)
        return Results.NotFound("Image not found.");

    var imagePath = image["FileName"].ToString();
    var fileName = Path.GetFileName(imagePath);
    var imageUrl = $"/uploads/{fileName}";

    // Return HTML that displays the image and title
    return Results.Text($@"
        <html>
        <body>
            <h1>{image["Title"]}</h1>
            <img src='{imageUrl}' alt='Uploaded Image' />
        </body>
        </html>", "text/html");
});





app.Run();