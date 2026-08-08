namespace TodoApp;

class Program
{
    static async Task Main(string[] args)
    {
        var server = new DumbHttpServer();
        await server.RunAsync("../../../site", 8000);
    }
}