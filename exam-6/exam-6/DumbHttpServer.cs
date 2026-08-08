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

        string pageName = Path.GetFileName(absolutePath);
        string httpMethod = context.Request.HttpMethod;
        
        if (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) && pageName.Equals("addTask", StringComparison.OrdinalIgnoreCase))
        {
            AddTask(context.Request);
            Redirect(context, "/index.html");
            return;
        }

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

    private void Redirect(HttpListenerContext context, string url)
    {
        context.Response.Redirect(url);
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

    private void SaveTasks(List<TodoTask> tasks)
    {
        string jsonPath = Path.Combine(_siteDirectory, "tasks.json");
        string json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);
    }

    private void AddTask(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var formData = ParseFormData(reader.ReadToEnd());

        var tasks = ReadTasks();
        int newId = tasks.Count > 0 ? tasks.Max(t => t.Id) + 1 : 1;

        var newTask = new TodoTask
        {
            Id = newId,
            Title = formData.GetValueOrDefault("title") ?? "",
            Assignee = formData.GetValueOrDefault("assignee") ?? "",
            Description = formData.GetValueOrDefault("description") ?? "",
            CreatedAt = DateTime.Now.ToString("dd.MM.yyyy"),
            CompletedAt = "-",
            Status = "new"
        };

        tasks.Add(newTask);
        SaveTasks(tasks);
    }

    private Dictionary<string, string> ParseFormData(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pairs = body.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=');
            if (parts.Length == 2)
            {
                string key = WebUtility.UrlDecode(parts[0]);
                string value = WebUtility.UrlDecode(parts[1]).Replace('+', ' ');
                result[key] = value;
            }
        }
        return result;
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