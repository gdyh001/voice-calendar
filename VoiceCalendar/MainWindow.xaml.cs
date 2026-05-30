using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VoiceCalendar.Models;
using VoiceCalendar.Services;
using VoiceCalendar.ViewModels;

namespace VoiceCalendar;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly VoiceService _voiceService = new();

    public MainWindow()
    {
        InitializeComponent();

        // 绑定 ViewModel
        DataContext = _vm;

        // 日历控件日期点击
        CalendarView.DateClicked += OnCalendarDateClicked;

        // 初始刷新
        CalendarView.GoToDate(DateTime.Today);
        UpdateDateHeader();
        RefreshEventList();

        // 监听 ViewModel 属性变化
        _vm.PropertyChanged += (_, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (e.PropertyName == nameof(MainViewModel.Events))
                    RefreshEventList();
                if (e.PropertyName == nameof(MainViewModel.SelectedDate))
                    UpdateDateHeader();
                if (e.PropertyName == nameof(MainViewModel.StatusText))
                    TxtStatus.Text = _vm.StatusText;
                if (e.PropertyName == nameof(MainViewModel.VoiceButtonText))
                    BtnVoice.Content = _vm.VoiceButtonText;
            });
        };

        TxtStatus.Text = "就绪 — 点击 🎤 开始语音输入";
    }

    // === 日历交互 ===

    private void OnCalendarDateClicked(DateTime date)
    {
        _vm.SelectedDate = date;
        TxtMonthTitle.Text = $"{CalendarView.CurrentMonth.Year}年{CalendarView.CurrentMonth.Month}月";
    }

    private void BtnPrevMonth_Click(object sender, RoutedEventArgs e)
    {
        CalendarView.GoToDate(CalendarView.CurrentMonth.AddMonths(-1));
        TxtMonthTitle.Text = $"{CalendarView.CurrentMonth.Year}年{CalendarView.CurrentMonth.Month}月";
    }

    private void BtnNextMonth_Click(object sender, RoutedEventArgs e)
    {
        CalendarView.GoToDate(CalendarView.CurrentMonth.AddMonths(1));
        TxtMonthTitle.Text = $"{CalendarView.CurrentMonth.Year}年{CalendarView.CurrentMonth.Month}月";
    }

    private void BtnToday_Click(object sender, RoutedEventArgs e)
    {
        CalendarView.GoToDate(DateTime.Today);
        TxtMonthTitle.Text = $"{CalendarView.CurrentMonth.Year}年{CalendarView.CurrentMonth.Month}月";
    }

    private void UpdateDateHeader()
    {
        var date = _vm.SelectedDate;
        TxtSelectedDate.Text = $"{date.Month}月{date.Day}日";
        string[] weekdays = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
        TxtSelectedWeekday.Text = weekdays[(int)date.DayOfWeek];
        TxtMonthTitle.Text = $"{CalendarView.CurrentMonth.Year}年{CalendarView.CurrentMonth.Month}月";
    }

    private void RefreshEventList()
    {
        EventList.ItemsSource = null;
        EventList.ItemsSource = _vm.Events;
    }

    // === 语音输入 ===

    private async void BtnVoice_Click(object sender, RoutedEventArgs e)
    {
        BtnVoice.IsEnabled = false;
        BtnVoice.Content = "正在聆听...";
        _vm.StatusText = "正在聆听...";

        try
        {
            var text = await _voiceService.ListenAsync(8000);
            if (text.StartsWith("["))
                _vm.StatusText = text;
            else
            {
                _vm.StatusText = $"识别: {text}";
                _vm.ProcessVoiceCommand(text);
                CalendarView.Refresh();
            }
        }
        catch (Exception ex)
        {
            _vm.StatusText = $"错误: {ex.Message}";
        }
        finally
        {
            BtnVoice.IsEnabled = true;
            BtnVoice.Content = "🎤 语音输入";
        }
    }
    // === 手动添加事件 ===

    private void BtnAddEvent_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddEventDialog(_vm.SelectedDate);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            _vm.AddEventManually(dialog.EventTitle, dialog.EventDate, dialog.EventTime);
            CalendarView.GoToDate(dialog.EventDate);
        }
    }

    // === 右键菜单 - 编辑 ===

    private void MenuItemEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is CalendarEvent ev)
        {
            var dialog = new EditEventDialog(ev);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _vm.UpdateEvent(ev.Id, dialog.EventTitle, dialog.EventDate, dialog.EventTime);
                CalendarView.Refresh();
            }
        }
    }

    // === 右键菜单 - 删除 ===

    private void MenuItemDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is CalendarEvent ev)
        {
            if (MessageBox.Show($"确定删除「{ev.Title}」？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _vm.DeleteEvent(ev.Id);
                CalendarView.Refresh();
            }
        }
    }
}

// ============================================================
// 添加事件对话框
// ============================================================
public class AddEventDialog : Window
{
    public string EventTitle { get; private set; } = "";
    public DateTime EventDate { get; private set; }
    public string? EventTime { get; private set; }

    private readonly TextBox _titleBox;
    private readonly DatePicker _datePicker;
    private readonly TextBox _timeBox;

    public AddEventDialog(DateTime defaultDate)
    {
        Title = "添加事件";
        Width = 360; Height = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // 标题
        var titleLbl = new TextBlock { Text = "事件名称", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)),
            Margin = new Thickness(0,0,0,4) };
        Grid.SetRow(titleLbl, 0); grid.Children.Add(titleLbl);

        _titleBox = new TextBox { FontSize = 14, Padding = new Thickness(8,6,8,6),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5,0xE5,0xEA)),
            BorderThickness = new Thickness(1) };
        Grid.SetRow(_titleBox, 1); grid.Children.Add(_titleBox);

        // 日期
        var dateLbl = new TextBlock { Text = "日期", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)),
            Margin = new Thickness(0,12,0,4) };
        Grid.SetRow(dateLbl, 2); grid.Children.Add(dateLbl);

        _datePicker = new DatePicker { SelectedDate = defaultDate, FontSize = 14 };
        Grid.SetRow(_datePicker, 3); grid.Children.Add(_datePicker);

        // 时间
        var timeLbl = new TextBlock { Text = "时间 (HH:mm，留空为全天)", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)),
            Margin = new Thickness(0,12,0,4) };
        Grid.SetRow(timeLbl, 4); grid.Children.Add(timeLbl);

        var timePanel = new StackPanel { Orientation = Orientation.Horizontal };
        _timeBox = new TextBox { FontSize = 14, Width = 80, Padding = new Thickness(8,6,8,6),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5,0xE5,0xEA)),
            BorderThickness = new Thickness(1) };
        timePanel.Children.Add(_timeBox);
        var clearBtn = new Button { Content = "清除", FontSize = 12, Margin = new Thickness(8,0,0,0),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8E,0x8E,0x93)),
            Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        clearBtn.Click += (_, _) => _timeBox.Text = "";
        timePanel.Children.Add(clearBtn);
        Grid.SetRow(timePanel, 5); grid.Children.Add(timePanel);

        // 按钮
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,16,0,0) };
        var cancelBtn = new Button { Content = "取消", FontSize = 14, Padding = new Thickness(16,6,16,6),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8E,0x8E,0x93)),
            Cursor = System.Windows.Input.Cursors.Hand };
        cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
        btnPanel.Children.Add(cancelBtn);

        var saveBtn = new Button { Content = "保存", FontSize = 14, FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(20,6,20,6), Margin = new Thickness(12,0,0,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00,0x7A,0xFF)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        saveBtn.Click += (_, _) =>
        {
            var title = _titleBox.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                MessageBox.Show("请输入事件名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            EventTitle = title;
            EventDate = _datePicker.SelectedDate?.Date ?? DateTime.Today;
            var time = _timeBox.Text.Trim();
            if (!string.IsNullOrEmpty(time))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(time, @"^\d{2}:\d{2}$"))
                {
                    MessageBox.Show("时间格式错误，请使用 HH:mm", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                EventTime = time;
            }
            DialogResult = true;
            Close();
        };
        btnPanel.Children.Add(saveBtn);

        var outerGrid = new Grid();
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(grid, 0); outerGrid.Children.Add(grid);
        Grid.SetRow(btnPanel, 6); outerGrid.Children.Add(btnPanel);

        Content = outerGrid;
    }
}

// ============================================================
// 编辑事件对话框
// ============================================================
public class EditEventDialog : Window
{
    public string EventTitle { get; private set; } = "";
    public DateTime EventDate { get; private set; }
    public string? EventTime { get; private set; }

    public EditEventDialog(CalendarEvent ev)
    {
        Title = "编辑事件";
        Width = 360; Height = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleLbl = new TextBlock { Text = "事件名称", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)),
            Margin = new Thickness(0,0,0,4) };
        Grid.SetRow(titleLbl, 0); grid.Children.Add(titleLbl);

        var titleBox = new TextBox { Text = ev.Title, FontSize = 14, Padding = new Thickness(8,6,8,6),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5,0xE5,0xEA)),
            BorderThickness = new Thickness(1) };
        Grid.SetRow(titleBox, 1); grid.Children.Add(titleBox);

        var dateLbl = new TextBlock { Text = "日期", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)),
            Margin = new Thickness(0,12,0,4) };
        Grid.SetRow(dateLbl, 2); grid.Children.Add(dateLbl);

        var datePicker = new DatePicker { SelectedDate = ev.EventDate, FontSize = 14 };
        Grid.SetRow(datePicker, 3); grid.Children.Add(datePicker);

        var timeLbl = new TextBlock { Text = "时间 (HH:mm，留空为全天)", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)),
            Margin = new Thickness(0,12,0,4) };
        Grid.SetRow(timeLbl, 4); grid.Children.Add(timeLbl);

        var timePanel = new StackPanel { Orientation = Orientation.Horizontal };
        var timeBox = new TextBox { Text = ev.EventTime ?? "", FontSize = 14, Width = 80, Padding = new Thickness(8,6,8,6),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5,0xE5,0xEA)),
            BorderThickness = new Thickness(1) };
        timePanel.Children.Add(timeBox);
        var clearBtn = new Button { Content = "清除", FontSize = 12, Margin = new Thickness(8,0,0,0),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8E,0x8E,0x93)),
            Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        clearBtn.Click += (_, _) => timeBox.Text = "";
        timePanel.Children.Add(clearBtn);
        Grid.SetRow(timePanel, 5); grid.Children.Add(timePanel);

        // 按钮
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,16,0,0) };

        var deleteBtn = new Button { Content = "删除", FontSize = 14, Padding = new Thickness(12,6,12,6),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF,0x3B,0x30)),
            Cursor = System.Windows.Input.Cursors.Hand };
        btnPanel.Children.Add(deleteBtn);

        var cancelBtn = new Button { Content = "取消", FontSize = 14, Padding = new Thickness(12,6,12,6),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8E,0x8E,0x93)),
            Cursor = System.Windows.Input.Cursors.Hand };
        cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
        btnPanel.Children.Add(cancelBtn);

        var saveBtn = new Button { Content = "保存", FontSize = 14, FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(16,6,16,6), Margin = new Thickness(12,0,0,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00,0x7A,0xFF)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        saveBtn.Click += (_, _) =>
        {
            var title = titleBox.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                MessageBox.Show("请输入事件名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            EventTitle = title;
            EventDate = datePicker.SelectedDate?.Date ?? DateTime.Today;
            var time = timeBox.Text.Trim();
            if (!string.IsNullOrEmpty(time))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(time, @"^\d{2}:\d{2}$"))
                {
                    MessageBox.Show("时间格式错误，请使用 HH:mm", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                EventTime = time;
            }
            DialogResult = true;
            Close();
        };
        btnPanel.Children.Add(saveBtn);

        var outerGrid = new Grid();
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        outerGrid.Children.Add(grid);
        Grid.SetRow(btnPanel, 6); outerGrid.Children.Add(btnPanel);

        Content = outerGrid;
    }
}
