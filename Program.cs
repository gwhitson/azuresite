using System.Text;
using Microsoft.AspNetCore.Mvc;
using MimeKit;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to allow larger request bodies (100MB limit)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
});

var app = builder.Build();

// Endpoint for parsing emails with large attachments
app.MapPost("/api/ParseEmailWithAttachments", async (HttpContext context) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        string rawBody = await reader.ReadToEndAsync();

        // Decode Base64 if needed
        string emlContent = IsBase64String(rawBody)
            ? Encoding.UTF8.GetString(Convert.FromBase64String(rawBody))
            : rawBody;

        using var emailStream = new MemoryStream(Encoding.UTF8.GetBytes(emlContent));
        var message = MimeMessage.Load(emailStream);

        // Stream attachments instead of loading them fully into memory
        var attachments = new List<object>();

        foreach (var attachment in message.Attachments)
        {
            if (attachment is MimePart mimePart)
            {
                using var attachmentStream = new MemoryStream();
                await mimePart.Content.DecodeToAsync(attachmentStream);
                
                // Convert to Base64 while minimizing memory footprint
                attachmentStream.Position = 0;
                using var readerStream = new StreamReader(attachmentStream);
                string base64Content = Convert.ToBase64String(attachmentStream.ToArray());
                string content = System.Text.Encoding.Default.GetString(Convert.FromBase64String(base64Content));
                //string content = Convert.ToString(attachmentStream);
                //string content = Convert.ToString(attachmentStream.ToArray());
                //string content = Convert.ToString(attachmentStream.ToArray());
                //Convert.ToString(attachmentStream.ToArray());
                //ToBase64String(attachmentStream.ToArray());

                attachments.Add(new
                {
                    FileName = mimePart.FileName,
                    ContentType = mimePart.ContentType.MimeType,
                    Content = content
                });
            }
        }

        // Construct response with streamed data
        var response = new
        {
            OriginalEmail = Convert.ToBase64String(Encoding.UTF8.GetBytes(emlContent)),
            Attachments = attachments
        };

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Helper method to check if a string is Base64-encoded
static bool IsBase64String(string input)
{
    if (string.IsNullOrEmpty(input)) return false;
    Span<byte> buffer = new Span<byte>(new byte[input.Length]);
    return Convert.TryFromBase64String(input, buffer, out _);
}

app.Run();
