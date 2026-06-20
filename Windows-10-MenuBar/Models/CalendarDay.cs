namespace Windows_10_MenuBar.Models;

public class CalendarDay
{
    public int Day { get; set; }           // 0 = boş hücre
    public bool IsToday { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsSpecialDay { get; set; }
    public bool IsWeekend { get; set; }
    public string? SpecialDayName { get; set; }
    public System.DateTime Date { get; set; }
}
