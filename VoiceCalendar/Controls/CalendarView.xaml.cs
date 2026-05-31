using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VoiceCalendar.Services;

namespace VoiceCalendar.Controls;

public partial class CalendarView : UserControl
{
    public DateTime CurrentMonth { get; private set; } = DateTime.Today;
    public DateTime SelectedDate { get; private set; } = DateTime.Today;

    private readonly EventStorageService _storage = EventStorageService.Instance;
    private readonly List<DayCell> _dayCells = new();

    private int _pickerYear;
    private bool _pickerIsForYear;

    public event Action<DateTime>? DateClicked;

    public CalendarView()
    {
        InitializeComponent();
        BuildDayGrid();
        Refresh();
    }

    public void Refresh()
    {
        TxtYear.Text = CurrentMonth.Year.ToString();
        TxtMonth.Text = $"{CurrentMonth.Month}月";
        var eventCounts = _storage.GetEventCountsForMonth(CurrentMonth.Year, CurrentMonth.Month);

        var firstDay = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
        int startOffset = ((int)firstDay.DayOfWeek + 6) % 7; // 0=周一
        int daysInMonth = DateTime.DaysInMonth(CurrentMonth.Year, CurrentMonth.Month);
        var today = DateTime.Today;

        int day = 1;
        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                int idx = row * 7 + col;
                if (idx >= _dayCells.Count) continue;

                var cell = _dayCells[idx];
                if (row == 0 && col < startOffset || day > daysInMonth)
                {
                    cell.SetEmpty();
                }
                else
                {
                    var date = new DateTime(CurrentMonth.Year, CurrentMonth.Month, day);
                    bool isToday = date == today;
                    bool isSelected = date == SelectedDate;
                    bool hasEvents = eventCounts.TryGetValue(day, out int count) && count > 0;
                    bool isWeekend = col >= 5;

                    cell.SetDate(CurrentMonth.Year, CurrentMonth.Month, day, isToday, isSelected, isWeekend, hasEvents, Math.Min(count, 3));
                    day++;
                }
            }
        }
    }

    public void GoToDate(DateTime date)
    {
        CurrentMonth = new DateTime(date.Year, date.Month, 1);
        SelectedDate = date.Date;
        Refresh();
        DateClicked?.Invoke(SelectedDate);
    }

    private void BtnPrev_Click(object sender, RoutedEventArgs e)
    {
        CurrentMonth = CurrentMonth.AddMonths(-1);
        Refresh();
    }

    private void BtnToday_Click(object sender, RoutedEventArgs e)
    {
        CurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        SelectedDate = DateTime.Today;
        Refresh();
        DateClicked?.Invoke(SelectedDate);
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        CurrentMonth = CurrentMonth.AddMonths(1);
        Refresh();
    }

    // === 年月选择器 ===

    private void TxtYear_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _pickerYear = CurrentMonth.Year;
        _pickerIsForYear = true;
        ShowYearPicker();
    }

    private void TxtMonth_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _pickerYear = CurrentMonth.Year;
        _pickerIsForYear = false;
        ShowMonthPicker();
    }

    private void ShowYearPicker()
    {
        DayGrid.Visibility = Visibility.Collapsed;
        MonthGrid.Visibility = Visibility.Collapsed;
        YearScrollViewer.Visibility = Visibility.Visible;
        PickerPanel.Visibility = Visibility.Visible;
        TxtPickerTitle.Text = "选择年份";

        YearGrid.Children.Clear();
        for (int y = 2010; y <= 2050; y++)
        {
            var btn = CreatePickerButton(y.ToString(), y == _pickerYear);
            int year = y;
            btn.MouseLeftButtonDown += (_, _) => YearButton_Click(year);
            YearGrid.Children.Add(btn);
        }

        // Scroll to current year
        YearScrollViewer.ScrollToTop();
        int row = (_pickerYear - 2010) / 3;
        double offset = row * 44.0;
        YearScrollViewer.ScrollToVerticalOffset(Math.Max(0, offset - 100));
    }

    private void ShowMonthPicker()
    {
        DayGrid.Visibility = Visibility.Collapsed;
        YearScrollViewer.Visibility = Visibility.Collapsed;
        MonthGrid.Visibility = Visibility.Visible;
        PickerPanel.Visibility = Visibility.Visible;
        TxtPickerTitle.Text = "选择月份";

        MonthGrid.Children.Clear();
        for (int m = 1; m <= 12; m++)
        {
            bool isCurrent = _pickerYear == CurrentMonth.Year && m == CurrentMonth.Month;
            var btn = CreatePickerButton($"{m}月", isCurrent);
            int month = m;
            btn.MouseLeftButtonDown += (_, _) => MonthButton_Click(month);
            MonthGrid.Children.Add(btn);
        }
    }

    private void PickerBack_Click(object sender, RoutedEventArgs e)
    {
        if (MonthGrid.Visibility == Visibility.Visible && _pickerIsForYear)
        {
            // From month picker back to year picker
            ShowYearPicker();
        }
        else
        {
            // From year picker or direct month picker: cancel
            PickerCancel_Click(sender, e);
        }
    }

    private void PickerCancel_Click(object sender, RoutedEventArgs e)
    {
        PickerPanel.Visibility = Visibility.Collapsed;
        DayGrid.Visibility = Visibility.Visible;
    }

    private void YearButton_Click(int year)
    {
        _pickerYear = year;
        ShowMonthPicker();
    }

    private void MonthButton_Click(int month)
    {
        CurrentMonth = new DateTime(_pickerYear, month, 1);
        // Adjust SelectedDate: keep same day if valid, else fall back to 1st
        int day = Math.Min(SelectedDate.Day, DateTime.DaysInMonth(_pickerYear, month));
        SelectedDate = new DateTime(_pickerYear, month, day);
        PickerPanel.Visibility = Visibility.Collapsed;
        DayGrid.Visibility = Visibility.Visible;
        Refresh();
        DateClicked?.Invoke(SelectedDate);
    }

    private static Border CreatePickerButton(string text, bool isSelected)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(2),
            Height = 40,
            Cursor = System.Windows.Input.Cursors.Hand,
            Background = isSelected
                ? new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xFF))
                : new SolidColorBrush(Colors.Transparent),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isSelected
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        if (!isSelected)
        {
            border.MouseEnter += (_, _) =>
                border.Background = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA));
            border.MouseLeave += (_, _) =>
                border.Background = new SolidColorBrush(Colors.Transparent);
        }

        return border;
    }

    private void BuildDayGrid()
    {
        _dayCells.Clear();
        DayGrid.Children.Clear();
        DayGrid.RowDefinitions.Clear();
        DayGrid.ColumnDefinitions.Clear();

        for (int i = 0; i < 7; i++)
            DayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 6; i++)
            DayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                var cell = new DayCell();
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, col);
                cell.MouseLeftButtonDown += (_, _) =>
                {
                    if (cell.Date.HasValue)
                    {
                        SelectedDate = cell.Date.Value;
                        Refresh();
                        DateClicked?.Invoke(SelectedDate);
                    }
                };
                _dayCells.Add(cell);
                DayGrid.Children.Add(cell);
            }
        }
    }
}

/// <summary>
/// 日历网格中的一个日期格
/// </summary>
public class DayCell : Border
{
    public DateTime? Date { get; private set; }

    private readonly TextBlock _dayText;
    private readonly StackPanel _dotsPanel;

    private static readonly Brush IosBlue = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xFF));
    private static readonly Brush IosGray = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93));
    private static readonly Brush Transparent = new SolidColorBrush(Colors.Transparent);
    private static readonly Brush White = new SolidColorBrush(Colors.White);
    private static readonly Brush DarkText = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));
    private static readonly Brush TodayBg = new SolidColorBrush(Color.FromRgb(0xE8, 0xF0, 0xFE));
    private static readonly Brush HoverBg = new SolidColorBrush(Color.FromRgb(0xE8, 0xF0, 0xFE));
    private bool _isSelected;
    private bool _isToday;

    public DayCell()
    {
        Background = Transparent;
        CornerRadius = new CornerRadius(8);
        Margin = new Thickness(1);
        Cursor = System.Windows.Input.Cursors.Hand;

        MouseEnter += (_, _) =>
        {
            if (!_isSelected && !_isToday)
                Background = HoverBg;
        };
        MouseLeave += (_, _) =>
        {
            if (!_isSelected && !_isToday)
                Background = Transparent;
        };

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _dayText = new TextBlock
        {
            Text = "",
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI")
        };
        stack.Children.Add(_dayText);

        _dotsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        stack.Children.Add(_dotsPanel);

        Child = stack;
    }

    public void SetEmpty()
    {
        Date = null;
        _dayText.Text = "";
        _dayText.Foreground = IosGray;
        _dotsPanel.Children.Clear();
        Background = Transparent;
    }

    public void SetDate(int year, int month, int day, bool isToday, bool isSelected, bool isWeekend, bool hasEvents, int dotCount)
    {
        Date = new DateTime(year, month, day);
        _isToday = isToday;
        _isSelected = isSelected;
        _dayText.Text = day.ToString();
        _dotsPanel.Children.Clear();

        // 颜色逻辑
        if (isSelected)
        {
            _dayText.Foreground = White;
            Background = IosBlue;
        }
        else if (isToday)
        {
            _dayText.Foreground = IosBlue;
            _dayText.FontWeight = FontWeights.Bold;
            Background = TodayBg;
        }
        else if (isWeekend)
        {
            _dayText.Foreground = IosGray;
            _dayText.FontWeight = FontWeights.Normal;
            Background = Transparent;
        }
        else
        {
            _dayText.Foreground = DarkText;
            _dayText.FontWeight = FontWeights.Normal;
            Background = Transparent;
        }

        // 事件标记点
        if (hasEvents && dotCount > 0)
        {
            for (int i = 0; i < dotCount; i++)
            {
                var dot = new Ellipse
                {
                    Width = 5,
                    Height = 5,
                    Fill = isSelected ? White : IosBlue,
                    Margin = new Thickness(1, 0, 1, 0)
                };
                _dotsPanel.Children.Add(dot);
            }
        }
    }
}
