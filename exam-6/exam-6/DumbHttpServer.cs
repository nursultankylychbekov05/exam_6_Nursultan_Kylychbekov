using System.Net;
using System.Text.Json;
using RazorEngine;
using RazorEngine.Templating;

namespace TodoApp;

public class DumbHttpServer
{
    private string _siteDirectory = "";
    private HttpListener _listener = new HttpListener();

    public async Task RunAsync(string path, int port)
    {
        _siteDirectory = path;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();

        Console.WriteLine($"Сервер запущен: http://localhost:{port}/index.html");
        await ListenAsync();
    }

    private async Task ListenAsync()
    {
        try
        {
            while (true)
            {
                HttpListenerContext context = await _listener.GetContextAsync();
                Process(context);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Stop();
        }
    }

    public void Process(HttpListenerContext context)
    {
        string absolutePath = context.Request.Url?.AbsolutePath ?? "/";
        if (absolutePath == "/") absolutePath = "/index.html";

        string relativePath = absolutePath.TrimStart('/');
        string filePath = Path.Combine(_siteDirectory, relativePath);

        if (File.Exists(filePath))
        {
            try
            {
                string content = filePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                    ? BuildHtml(filePath)
                    : File.ReadAllText(filePath);

                context.Response.ContentType = GetContentType(filePath);
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
                context.Response.ContentLength64 = bytes.Length;
                context.Response.StatusCode = (int)HttpStatusCode.OK;

                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Flush();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Ошибка обработки HTML: {e.Message}");
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        }

        context.Response.OutputStream.Close();
    }

    private string BuildHtml(string filename)
    {
        string layoutPath = Path.Combine(_siteDirectory, "layout.html");
        var razorService = Engine.Razor;

        if (!razorService.IsTemplateCached(razorService.GetKey("layout"), null))
        {
            razorService.AddTemplate("layout", File.ReadAllText(layoutPath));
        }

        if (!razorService.IsTemplateCached(razorService.GetKey(filename), null))
        {
            razorService.AddTemplate(filename, File.ReadAllText(filename));
            razorService.Compile(filename);
        }

        var tasks = ReadTasks();
        return razorService.Run(filename, null, tasks);
    }

    private List<TodoTask> ReadTasks()
    {
        string jsonPath = Path.Combine(_siteDirectory, "tasks.json");
        if (!File.Exists(jsonPath)) return new List<TodoTask>();

        string jsonContent = File.ReadAllText(jsonPath);
        return JsonSerializer.Deserialize<List<TodoTask>>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<TodoTask>();
    }

    private string GetContentType(string filename)
    {
        string extension = Path.GetExtension(filename);
        return extension.ToLower() switch
        {
            ".css" => "text/css",
            ".html" => "text/html; charset=utf-8",
            ".json" => "application/json",
            _ => "text/plain"
        };
    }

    public void Stop()
    {
        _listener.Abort();
        _listener.Stop();
    }
}