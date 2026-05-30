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
        DataContext = _vm;

        CalendarView.DateClicked += OnCalendarDateClicked;
        CalendarView.GoToDate(DateTime.Today);
        UpdateDateHeader();
        RefreshEventList();

        _vm.PropertyChanged += (_, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (e.PropertyName == nameof(MainViewModel.Events)) RefreshEventList();
                if (e.PropertyName == nameof(MainViewModel.SelectedDate)) UpdateDateHeader();
                if (e.PropertyName == nameof(MainViewModel.StatusText)) TxtStatus.Text = _vm.StatusText;
                if (e.PropertyName == nameof(MainViewModel.VoiceButtonText)) BtnVoice.Content = _vm.VoiceButtonText;
            });
        };

        TxtStatus.Text = "就绪 — 可以用文字指令或语音";
    }

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
        string[] wds = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
        TxtSelectedWeekday.Text = wds[(int)date.DayOfWeek];
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

    // === 文字指令输入 ===
    private void BtnSendCommand_Click(object sender, RoutedEventArgs e) => ExecuteTextCommand();

    private void TxtCommand_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            ExecuteTextCommand();
            e.Handled = true;
        }
    }

    private void ExecuteTextCommand()
    {
        var text = TxtCommand.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        _vm.StatusText = $"指令: {text}";
        _vm.ProcessVoiceCommand(text);
        CalendarView.Refresh();
        TxtCommand.Text = "";
    }

    // === 手动添加 ===
    private void BtnAddEvent_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddEventDialog(_vm.SelectedDate) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _vm.AddEventManually(dlg.EventTitle, dlg.EventDate, dlg.EventTime);
            CalendarView.GoToDate(dlg.EventDate);
        }
    }

    private void MenuItemEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is CalendarEvent ev)
        {
            var dlg = new EditEventDialog(ev) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _vm.UpdateEvent(ev.Id, dlg.EventTitle, dlg.EventDate, dlg.EventTime);
                CalendarView.Refresh();
            }
        }
    }

    private void MenuItemDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is CalendarEvent ev)
        {
            if (MessageBox.Show($"确定删除「{ev.Title}」?", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _vm.DeleteEvent(ev.Id);
                CalendarView.Refresh();
            }
        }
    }
}

// ===== 对话框 =====

public class AddEventDialog : Window
{
    public string EventTitle { get; private set; } = "";
    public DateTime EventDate { get; private set; }
    public string? EventTime { get; private set; }
    private readonly TextBox _tb;
    private readonly DatePicker _dp;
    private readonly TextBox _tm;

    public AddEventDialog(DateTime defDate)
    {
        Title = "添加事件"; Width = 360; Height = 260;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;

        var g = new Grid { Margin = new Thickness(20) };
        for (int i = 0; i < 7; i++) g.RowDefinitions.Add(new RowDefinition { Height = i < 6 ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });

        g.Children.Add(Label("事件名称", 0, 0, 0, 4));
        _tb = TextBox(""); Grid.SetRow(_tb, 1); g.Children.Add(_tb);

        g.Children.Add(Label("日期", 2, 12, 0, 4));
        _dp = new DatePicker { SelectedDate = defDate, FontSize = 14 }; Grid.SetRow(_dp, 3); g.Children.Add(_dp);

        g.Children.Add(Label("时间 (HH:mm，留空=全天)", 4, 12, 0, 4));
        _tm = TextBox("", 80); Grid.SetRow(_tm, 5); g.Children.Add(_tm);

        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = Btn("取消", "#8E8E93", () => { DialogResult = false; Close(); });
        var save = Btn("保存", "#007AFF", () => {
            var t = _tb.Text.Trim();
            if (string.IsNullOrEmpty(t)) { MessageBox.Show("请输入事件名称"); return; }
            EventTitle = t;
            EventDate = _dp.SelectedDate?.Date ?? DateTime.Today;
            var tm = _tm.Text.Trim();
            if (!string.IsNullOrEmpty(tm) && !System.Text.RegularExpressions.Regex.IsMatch(tm, @"^\d{2}:\d{2}$"))
            { MessageBox.Show("时间格式 HH:mm"); return; }
            EventTime = string.IsNullOrEmpty(tm) ? null : tm;
            DialogResult = true; Close();
        });
        btns.Children.Add(cancel); btns.Children.Add(save);
        Grid.SetRow(btns, 6); g.Children.Add(btns);
        Content = g;
    }

    private static TextBlock Label(string text, int row, int top, int btm, int bot)
        => new() { Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)),
            Margin = new Thickness(0, top, btm, bot) };

    private static TextBox TextBox(string text, int w = 0)
        => new() { Text = text, FontSize = 14, Padding = new Thickness(8, 6, 8, 6), Width = w > 0 ? w : double.NaN,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5,0xE5,0xEA)),
            BorderThickness = new Thickness(1) };

    private static Button Btn(string text, string color, Action click)
    {
        var b = new Button { Content = text, FontSize = 14, Padding = new Thickness(16, 6, 16, 6),
            Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)),
            Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(8, 0, 0, 0) };
        b.Click += (_, _) => click();
        return b;
    }
}

public class EditEventDialog : Window
{
    public string EventTitle { get; private set; } = "";
    public DateTime EventDate { get; private set; }
    public string? EventTime { get; private set; }
    private readonly TextBox _tb;
    private readonly DatePicker _dp;
    private readonly TextBox _tm;

    public EditEventDialog(CalendarEvent ev)
    {
        Title = "编辑事件"; Width = 360; Height = 260;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;

        var g = new Grid { Margin = new Thickness(20) };
        for (int i = 0; i < 7; i++) g.RowDefinitions.Add(new RowDefinition { Height = i < 6 ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });

        g.Children.Add(new TextBlock { Text = "事件名称", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)),
            Margin = new Thickness(0, 0, 0, 4) });
        _tb = new TextBox { Text = ev.Title, FontSize = 14, Padding = new Thickness(8, 6, 8, 6),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5,0xE5,0xEA)),
            BorderThickness = new Thickness(1) };
        Grid.SetRow(_tb, 1); g.Children.Add(_tb);

        g.Children.Add(new TextBlock { Text = "日期", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)),
            Margin = new Thickness(0, 12, 0, 4) });
        _dp = new DatePicker { SelectedDate = ev.EventDate, FontSize = 14 };
        Grid.SetRow(_dp, 3); g.Children.Add(_dp);

        g.Children.Add(new TextBlock { Text = "时间 (HH:mm)", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)),
            Margin = new Thickness(0, 12, 0, 4) });
        _tm = new TextBox { Text = ev.EventTime ?? "", FontSize = 14, Width = 80, Padding = new Thickness(8, 6, 8, 6),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5,0xE5,0xEA)),
            BorderThickness = new Thickness(1) };
        Grid.SetRow(_tm, 5); g.Children.Add(_tm);

        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "取消", FontSize = 14, Padding = new Thickness(12, 6, 12, 6),
            Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8E,0x8E,0x93)),
            Cursor = System.Windows.Input.Cursors.Hand };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        var save = new Button { Content = "保存", FontSize = 14, FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(8, 0, 0, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00,0x7A,0xFF)),
            Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        save.Click += (_, _) => {
            var t = _tb.Text.Trim();
            if (string.IsNullOrEmpty(t)) { MessageBox.Show("请输入事件名称"); return; }
            EventTitle = t;
            EventDate = _dp.SelectedDate?.Date ?? DateTime.Today;
            var tm = _tm.Text.Trim();
            if (!string.IsNullOrEmpty(tm) && !System.Text.RegularExpressions.Regex.IsMatch(tm, @"^\d{2}:\d{2}$"))
            { MessageBox.Show("时间格式 HH:mm"); return; }
            EventTime = string.IsNullOrEmpty(tm) ? null : tm;
            DialogResult = true; Close();
        };
        btns.Children.Add(cancel); btns.Children.Add(save);
        Grid.SetRow(btns, 6); g.Children.Add(btns);
        Content = g;
    }
}