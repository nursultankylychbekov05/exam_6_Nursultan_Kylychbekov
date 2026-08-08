namespace TodoApp;

public class StatsViewModel
{
    public int TotalCount { get; set; }
    public int DoneCount { get; set; }
    public int NewCount { get; set; }
    public double DonePercentage => TotalCount > 0 ? Math.Round((double)DoneCount / TotalCount * 100, 1) : 0;
    public double NewPercentage => TotalCount > 0 ? Math.Round((double)NewCount / TotalCount * 100, 1) : 0;
}