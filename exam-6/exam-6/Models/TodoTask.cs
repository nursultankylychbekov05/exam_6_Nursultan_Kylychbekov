namespace TodoApp;

public class TodoTask
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Assignee { get; set; } = "";
    public string Description { get; set; } = "";
    public string CreatedAt { get; set; } = DateTime.Now.ToString("dd.MM.yyyy");
    public string? CompletedAt { get; set; } = "-";
    public string Status { get; set; } = "new"; 
}