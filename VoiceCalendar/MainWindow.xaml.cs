using System;
using System.IO;
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
    private static string FindModelPath()
    {
        var modelDir = Path.Combine("model", "vosk-model-small-cn-0.22");
        var candidates = new[] {
            Directory.GetCurrentDirectory(),
            AppDomain.CurrentDomain.BaseDirectory,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."),
        };
        foreach (var basePath in candidates)
        {
            var full = Path.GetFullPath(Path.Combine(basePath, modelDir));
            if (Directory.Exists(full)) return full;
        }
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), modelDir));
    }

    private readonly VoiceService _voiceService = new(FindModelPath());
    private System.Windows.Threading.DispatcherTimer? _recordTimer;
    private DateTime _scheduleDate;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // 加载 Vosk 语音模型
        if (!_voiceService.Initialize())
        {
            _vm.StatusText = "语音模型未加载，请下载 vosk-model-small-cn-0.22 到 model/ 目录";
        }

        CalendarView.DateClicked += (date) =>
        {
            _vm.SelectedDate = date;
            PopulateSchedule(date);
        };
        CalendarView.GoToDate(DateTime.Today);
        PopulateSchedule(DateTime.Today);
    }

    // === 窗口 ===
    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    { if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove(); }
    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        BtnMaximize.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    // === 日程 ===
    private void PopulateSchedule(DateTime date)
    {
        _scheduleDate = date;
        string[] wds = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
        TxtScheduleDate.Text = $"{date.Month}月{date.Day}日";
        TxtScheduleWeekday.Text = wds[(int)date.DayOfWeek];

        var events = _vm.GetEventsForDate(date);
        if (events.Count == 0)
        {
            TxtEmptySchedule.Visibility = Visibility.Visible;
            ScheduleList.Visibility = Visibility.Collapsed;
        }
        else
        {
            TxtEmptySchedule.Visibility = Visibility.Collapsed;
            ScheduleList.Visibility = Visibility.Visible;
            ScheduleList.ItemsSource = events;
        }
    }

    // === + 新建 ===
    private void BtnNewFromSchedule_Click(object sender, RoutedEventArgs e)
    {
        // 切换到表单
        SchedulePanel.Visibility = Visibility.Collapsed;
        FormPanel.Visibility = Visibility.Visible;
        BtnNewFromSchedule.Visibility = Visibility.Collapsed;
        FormButtons.Visibility = Visibility.Visible;

        DpStartDate.SelectedDate = _scheduleDate;
        DpEndDate.SelectedDate = _scheduleDate;
        TxtEventTitle.Text = "";
        CmbStartHour.SelectedIndex = 0;
        CmbStartMin.SelectedIndex = 0;
        CmbEndHour.SelectedIndex = 0;
        CmbEndMin.SelectedIndex = 0;
    }

    private void BtnCancelForm_Click(object sender, RoutedEventArgs e)
    {
        SchedulePanel.Visibility = Visibility.Visible;
        FormPanel.Visibility = Visibility.Collapsed;
        BtnNewFromSchedule.Visibility = Visibility.Visible;
        FormButtons.Visibility = Visibility.Collapsed;
    }

    private void BtnSaveForm_Click(object sender, RoutedEventArgs e)
    {
        var title = TxtEventTitle.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            MessageBox.Show("请输入日程名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var startDate = DpStartDate.SelectedDate?.Date ?? _scheduleDate;
        var sh = (CmbStartHour.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var sm = (CmbStartMin.SelectedItem as ComboBoxItem)?.Content?.ToString();
        string? startTime = string.IsNullOrEmpty(sh) ? null : $"{sh}:{sm ?? "00"}";

        var endDate = DpEndDate.SelectedDate?.Date;
        var eh = (CmbEndHour.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var em = (CmbEndMin.SelectedItem as ComboBoxItem)?.Content?.ToString();
        string? endTime = string.IsNullOrEmpty(eh) ? null : $"{eh}:{em ?? "00"}";

        _vm.AddEventManually(title, startDate, startTime);
        if (endDate.HasValue && endDate.Value > startDate)
            for (var d = startDate.AddDays(1); d <= endDate.Value; d = d.AddDays(1))
                _vm.AddEventManually(title, d, null);

        CalendarView.Refresh();
        BtnCancelForm_Click(sender, e);
        PopulateSchedule(_scheduleDate);
    }

    // === 语音 ===
    private void BtnVoice_Click(object sender, RoutedEventArgs e)
    {
        
        if (_voiceService.IsListening)
        {
            StopAndProcess();
        }
        else
        {
            StartRecording();
        }
    }

    private async void StartRecording()
    {
        BtnVoiceIdle.Visibility = Visibility.Collapsed;
        BtnVoiceRecording.Visibility = Visibility.Visible;;

        _recordTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _recordTimer.Tick += (_, _) =>
        {
            _recordTimer.Stop();
            StopAndProcess();
        };
        _recordTimer.Start();

        try
        {
            await _voiceService.StartRecordingAsync();
        }
        catch (InvalidOperationException ex)
        {
            _recordTimer.Stop();
            BtnVoiceIdle.Visibility = Visibility.Visible;
            BtnVoiceRecording.Visibility = Visibility.Collapsed;
            MessageBox.Show(ex.Message, "模型未找到");
        }
        catch (UnauthorizedAccessException)
        {
            _recordTimer.Stop();
            BtnVoiceIdle.Visibility = Visibility.Visible;
            BtnVoiceRecording.Visibility = Visibility.Collapsed;
            MessageBox.Show("无法访问麦克风。\n请检查：设置→隐私和安全性→麦克风→允许应用访问", "录音失败");
        }
        catch (Exception ex)
        {
            _recordTimer.Stop();
            BtnVoiceIdle.Visibility = Visibility.Visible;
            BtnVoiceRecording.Visibility = Visibility.Collapsed;
            MessageBox.Show($"语音引擎错误：{ex.Message}", "语音错误");
        }


    }
    private void StopAndProcess()
    {
        _recordTimer?.Stop();
        var text = _voiceService.StopRecording();
        BtnVoiceIdle.Visibility = Visibility.Visible;
        BtnVoiceRecording.Visibility = Visibility.Collapsed;

        if (!string.IsNullOrEmpty(text) && !text.StartsWith("["))
        {
            _vm.ProcessVoiceCommand(text);
            CalendarView.Refresh();
            PopulateSchedule(_scheduleDate);
        }
    }

    // === 右键 ===
    private void MenuItemEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is CalendarEvent ev)
        {
            var dlg = new EditEventDialog(ev) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _vm.UpdateEvent(ev.Id, dlg.EventTitle, dlg.EventDate, dlg.EventTime);
                CalendarView.Refresh();
                PopulateSchedule(_scheduleDate);
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
                PopulateSchedule(_scheduleDate);
            }
        }
    }
}

public class AddEventDialog : Window
{
    public string EventTitle { get; private set; } = "";
    public DateTime EventDate { get; private set; }
    public string? EventTime { get; private set; }
    public AddEventDialog(DateTime defDate)
    {
        Title = "新建日程"; Width = 360; Height = 260;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        var g = new Grid { Margin = new Thickness(20) };
        for (int i = 0; i < 7; i++) g.RowDefinitions.Add(new RowDefinition { Height = i < 6 ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
        g.Children.Add(Lbl("日程名称", 0, 0, 4)); var tb = Tb(""); Grid.SetRow(tb, 1); g.Children.Add(tb);
        g.Children.Add(Lbl("日期", 12, 0, 4)); var dp = new DatePicker { SelectedDate = defDate, FontSize = 14 }; Grid.SetRow(dp, 3); g.Children.Add(dp);
        g.Children.Add(Lbl("时间 (HH:mm)", 12, 0, 4)); var tm = Tb("", 80); Grid.SetRow(tm, 5); g.Children.Add(tm);
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = Btn("取消", "#8E8E93", () => { DialogResult = false; Close(); });
        var save = Btn("保存", "#007AFF", () => { var t = tb.Text.Trim(); if (string.IsNullOrEmpty(t)) { MessageBox.Show("请输入日程名称"); return; } EventTitle = t; EventDate = dp.SelectedDate?.Date ?? DateTime.Today; var t2 = tm.Text.Trim(); if (!string.IsNullOrEmpty(t2) && !System.Text.RegularExpressions.Regex.IsMatch(t2, @"^\d{2}:\d{2}$")) { MessageBox.Show("时间格式 HH:mm"); return; } EventTime = string.IsNullOrEmpty(t2) ? null : t2; DialogResult = true; Close(); });
        btns.Children.Add(cancel); btns.Children.Add(save); Grid.SetRow(btns, 6); g.Children.Add(btns); Content = g;
    }
    static TextBlock Lbl(string t, int top, int btm, int bot) => new() { Text = t, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)), Margin = new Thickness(0, top, btm, bot) };
    static TextBox Tb(string t, int w = 0) => new() { Text = t, FontSize = 14, Padding = new Thickness(8, 6, 8, 6), Width = w > 0 ? w : double.NaN, BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5,0xE5,0xEA)), BorderThickness = new Thickness(1) };
    static Button Btn(string t, string c, Action click) { var b = new Button { Content = t, FontSize = 14, Padding = new Thickness(16, 6, 16, 6), Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(c)), Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(8, 0, 0, 0) }; b.Click += (_, _) => click(); return b; }
}

public class EditEventDialog : Window
{
    public string EventTitle { get; private set; } = "";
    public DateTime EventDate { get; private set; }
    public string? EventTime { get; private set; }
    public EditEventDialog(CalendarEvent ev)
    {
        Title = "编辑日程"; Width = 360; Height = 260;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        var g = new Grid { Margin = new Thickness(20) };
        for (int i = 0; i < 7; i++) g.RowDefinitions.Add(new RowDefinition { Height = i < 6 ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
        g.Children.Add(Lbl("日程名称", 0, 0, 4)); var tb = Tb(ev.Title); Grid.SetRow(tb, 1); g.Children.Add(tb);
        g.Children.Add(Lbl("日期", 12, 0, 4)); var dp = new DatePicker { SelectedDate = ev.EventDate, FontSize = 14 }; Grid.SetRow(dp, 3); g.Children.Add(dp);
        g.Children.Add(Lbl("时间 (HH:mm)", 12, 0, 4)); var tm = Tb(ev.EventTime ?? "", 80); Grid.SetRow(tm, 5); g.Children.Add(tm);
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "取消", FontSize = 14, Padding = new Thickness(12, 6, 12, 6), Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8E,0x8E,0x93)), Cursor = System.Windows.Input.Cursors.Hand };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        var save = new Button { Content = "保存", FontSize = 14, FontWeight = FontWeights.SemiBold, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(8, 0, 0, 0), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00,0x7A,0xFF)), Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        save.Click += (_, _) => { var t = tb.Text.Trim(); if (string.IsNullOrEmpty(t)) { MessageBox.Show("请输入日程名称"); return; } EventTitle = t; EventDate = dp.SelectedDate?.Date ?? DateTime.Today; var t2 = tm.Text.Trim(); if (!string.IsNullOrEmpty(t2) && !System.Text.RegularExpressions.Regex.IsMatch(t2, @"^\d{2}:\d{2}$")) { MessageBox.Show("时间格式 HH:mm"); return; } EventTime = string.IsNullOrEmpty(t2) ? null : t2; DialogResult = true; Close(); };
        btns.Children.Add(cancel); btns.Children.Add(save); Grid.SetRow(btns, 6); g.Children.Add(btns); Content = g;
    }
    static TextBlock Lbl(string t, int top, int btm, int bot) => new() { Text = t, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)), Margin = new Thickness(0, top, btm, bot) };
    static TextBox Tb(string t, int w = 0) => new() { Text = t, FontSize = 14, Padding = new Thickness(8, 6, 8, 6), Width = w > 0 ? w : double.NaN, BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5,0xE5,0xEA)), BorderThickness = new Thickness(1) };
}
