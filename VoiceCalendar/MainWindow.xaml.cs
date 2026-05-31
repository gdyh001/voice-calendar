using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Controls;
using VoiceCalendar.Models;
using VoiceCalendar.Services;
using VoiceCalendar.ViewModels;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace VoiceCalendar;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly NlpParserService _nlp = new();

    private readonly VoiceService _voiceService = new("");
    private System.Windows.Threading.DispatcherTimer? _recordTimer;
    private DateTime _scheduleDate;
    private System.Windows.Threading.DispatcherTimer? _modelIdleTimer;
    private enum EditMode { None, Start, End }
    private EditMode _editMode = EditMode.None;
    private DateTime _startDateTime;
    private DateTime _endDateTime;
    private bool _suppressEvents;

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("user32.dll")]
    private static extern int DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private bool _isWindows11;
    private IntPtr _currentRegion = IntPtr.Zero;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;


        _voiceService.PartialResultChanged += (text) => Dispatcher.Invoke(() => TxtLiveText.Text = text);
        // 语音引擎按需初始化
        if (!_voiceService.Initialize())
        {
        // 语音引擎按需初始化
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;

        _isWindows11 = Environment.OSVersion.Version.Build >= 22000;

        if (_isWindows11)
        {
            var preference = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        else
        {
            ApplyRoundedCorners(hwnd);
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
            StateChanged += (_, _) =>
            {
                if (WindowState == WindowState.Maximized)
                    RemoveRoundedCorners(hwnd);
                else
                    ApplyRoundedCorners(hwnd);
            };
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_SIZE = 0x0005;
        if (msg == WM_SIZE && !_isWindows11)
        {
            if (WindowState == WindowState.Maximized)
                RemoveRoundedCorners(hwnd);
            else
                ApplyRoundedCorners(hwnd);
        }
        return IntPtr.Zero;
    }

    private void ApplyRoundedCorners(IntPtr hwnd)
    {
        RemoveRoundedCorners(hwnd);
        GetWindowRect(hwnd, out var rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        int radius = 12;
        _currentRegion = CreateRoundRectRgn(0, 0, width + 1, height + 1, radius, radius);
        SetWindowRgn(hwnd, _currentRegion, true);
    }

    private void RemoveRoundedCorners(IntPtr hwnd)
    {
        if (_currentRegion != IntPtr.Zero)
        {
            DeleteObject(_currentRegion);
            _currentRegion = IntPtr.Zero;
            SetWindowRgn(hwnd, IntPtr.Zero, true);
        }
    }

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

        TxtEventTitle.Text = "";
        _startDateTime = _scheduleDate.Date.AddHours(4);
        _endDateTime = _scheduleDate.Date.AddHours(5);
        _editMode = EditMode.None;
        RefreshTimeDisplays();
        LeaveEditMode();
    }

    private void BtnCancelForm_Click(object sender, RoutedEventArgs e)
    {
        SchedulePanel.Visibility = Visibility.Visible;
        FormPanel.Visibility = Visibility.Collapsed;
        TxtLiveText.Text = "";
        BtnNewFromSchedule.Visibility = Visibility.Visible;
        FormButtons.Visibility = Visibility.Collapsed;
    }

    private void BtnSaveForm_Click(object sender, RoutedEventArgs e)
    {
        var title = TxtEventTitle.Text.Trim();
        if (string.IsNullOrEmpty(title))
            title = "日程";

        var startDate = _startDateTime.Date;
        var startTime = $"{_startDateTime.Hour:D2}:{_startDateTime.Minute:D2}";
        
        

        var endDate = _endDateTime.Date;
        string? endTime = $"{_endDateTime.Hour:D2}:{_endDateTime.Minute:D2}";
        
        

        _vm.AddEventManually(title, startDate, startTime);
        if (endDate > startDate)
            for (var d = startDate.AddDays(1); d <= endDate; d = d.AddDays(1))
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
        BtnVoiceRecording.Visibility = Visibility.Visible;
        BtnVoiceRecording.ApplyTemplate();
        var label = BtnVoiceRecording.Template.FindName("RecordingLabel", BtnVoiceRecording) as System.Windows.Controls.TextBlock;
        if (label != null) label.Text = "准备中...";
        TxtLiveText.Visibility = Visibility.Collapsed;

        await Task.Delay(300);

        if (label != null) label.Text = "结束录制";
        TxtLiveText.Visibility = Visibility.Visible;
        TxtLiveText.Text = "识别中...";

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
            TxtLiveText.Visibility = Visibility.Collapsed;
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
    private void ResetModelIdleTimer() { _modelIdleTimer?.Stop(); _modelIdleTimer?.Start(); }

    private async void StopAndProcess()
    {
        _recordTimer?.Stop();
        var text = await _voiceService.StopRecordingAsync();
        ResetModelIdleTimer();
        BtnVoiceIdle.Visibility = Visibility.Visible;
        BtnVoiceRecording.Visibility = Visibility.Collapsed;
        TxtLiveText.Text = "";

        if (!string.IsNullOrEmpty(text) && !text.StartsWith("["))
        {
            AutoFillForm(text);
        }
    }

    private void AutoFillForm(string text)
    {
        var parsed = _nlp.Parse(text);

        if (parsed.Action == "delete" || parsed.Action == "query")
        {
            _vm.ProcessVoiceCommand(text);
            CalendarView.GoToDate(_vm.SelectedDate);
            PopulateSchedule(_vm.SelectedDate);
            TxtLiveText.Text = "";
            return;
        }

        var today = DateTime.Today;

        TxtEventTitle.Text = parsed.Title;
        _startDateTime = (parsed.Date ?? today).Date.AddHours(parsed.Hour ?? 4).AddMinutes(parsed.Minute ?? 0);
        if (parsed.EndHour.HasValue)
            _endDateTime = (parsed.Date ?? today).Date.AddHours(parsed.EndHour.Value).AddMinutes(parsed.EndMinute ?? 0);
        else
            _endDateTime = _startDateTime.AddHours(1);

        _editMode = EditMode.None;
        RefreshTimeDisplays();
        LeaveEditMode();


        SchedulePanel.Visibility = Visibility.Collapsed;
        FormPanel.Visibility = Visibility.Visible;
        BtnNewFromSchedule.Visibility = Visibility.Collapsed;
        FormButtons.Visibility = Visibility.Visible;

        _vm.StatusText = $"语音识别: {text}";
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


    // === iOS 时间选择器 ===
    private void RefreshTimeDisplays()
    {
        if (TxtStartDisplay != null)
            TxtStartDisplay.Text = FormatDateTime(_startDateTime);
        if (TxtEndDisplay != null)
            TxtEndDisplay.Text = FormatDateTime(_endDateTime);
    }

    private static string FormatDateTime(DateTime dt)
    {
        var ampm = dt.Hour < 12 ? "上午" : "下午";
        var h = dt.Hour % 12;
        if (h == 0) h = 12;
        return $"{dt.Month}月{dt.Day}日 {ampm}{h}:{dt.Minute:D2}";
    }

    private void SyncPickersToEditMode()
    {
        if (CmbAmPm == null || CmbHour == null || CmbMinute == null) return;
        _suppressEvents = true;
        var dt = _editMode == EditMode.Start ? _startDateTime : _endDateTime;
        CmbAmPm.SelectedIndex = dt.Hour < 12 ? 0 : 1;
        var h = dt.Hour % 12;
        if (h == 0) h = 12;
        CmbHour.SelectedIndex = h - 1;
        CmbMinute.SelectedIndex = dt.Minute / 5;
        _suppressEvents = false;
    }

    private void ApplyPickerValues()
    {
        if (CmbAmPm == null || CmbHour == null || CmbMinute == null) return;
        var isPM = CmbAmPm.SelectedIndex == 1;
        var h = CmbHour.SelectedIndex + 1; // 1-12
        if (isPM && h == 12) h = 12;
        else if (isPM) h += 12;
        else if (!isPM && h == 12) h = 0;
        var m = CmbMinute.SelectedIndex * 5;

        if (_editMode == EditMode.Start)
        {
            _startDateTime = _startDateTime.Date.AddHours(h).AddMinutes(m);
            if (_startDateTime > _endDateTime)
                _endDateTime = _startDateTime;
        }
        else
        {
            _endDateTime = _endDateTime.Date.AddHours(h).AddMinutes(m);
            if (_endDateTime < _startDateTime)
                _endDateTime = _startDateTime;
        }
        RefreshTimeDisplays();
    }

    private void EnterEditMode(EditMode mode)
    {
        if (_editMode == mode) { LeaveEditMode(); return; }
        _editMode = mode;
        // Highlight rows
        if (StartRow != null) StartRow.Background = mode == EditMode.Start
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE3, 0xF2, 0xFD))
            : System.Windows.Media.Brushes.Transparent;
        if (EndRow != null) EndRow.Background = mode == EditMode.End
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE3, 0xF2, 0xFD))
            : System.Windows.Media.Brushes.Transparent;
        if (TimePickerPanel != null) TimePickerPanel.Visibility = Visibility.Visible;
        SyncPickersToEditMode();
    }

    private void LeaveEditMode()
    {
        _editMode = EditMode.None;
        if (StartRow != null) StartRow.Background = System.Windows.Media.Brushes.Transparent;
        if (EndRow != null) EndRow.Background = System.Windows.Media.Brushes.Transparent;
        if (TimePickerPanel != null) TimePickerPanel.Visibility = Visibility.Collapsed;
    }

    private void StartRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_suppressEvents) return;
        e.Handled = true;
        EnterEditMode(EditMode.Start);
    }

    private void EndRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_suppressEvents) return;
        e.Handled = true;
        EnterEditMode(EditMode.End);
    }

    private void TimePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;
        ApplyPickerValues();
    }

    private void HiddenDatePicker_DateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || HiddenDatePicker == null) return;
        if (_editMode == EditMode.Start)
        {
            var t = _startDateTime.TimeOfDay;
            _startDateTime = (HiddenDatePicker.SelectedDate ?? DateTime.Today).Date + t;
            if (_startDateTime > _endDateTime) _endDateTime = _startDateTime;
        }
        else if (_editMode == EditMode.End)
        {
            var t = _endDateTime.TimeOfDay;
            _endDateTime = (HiddenDatePicker.SelectedDate ?? DateTime.Today).Date + t;
            if (_endDateTime < _startDateTime) _endDateTime = _startDateTime;
        }
        RefreshTimeDisplays();
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
        var save = Btn("保存", "#007AFF", () => { var t = tb.Text.Trim(); if (string.IsNullOrEmpty(t)) { t = "日程"; } EventTitle = t; EventDate = dp.SelectedDate?.Date ?? DateTime.Today; var t2 = tm.Text.Trim(); if (!string.IsNullOrEmpty(t2) && !System.Text.RegularExpressions.Regex.IsMatch(t2, @"^\d{2}:\d{2}$")) { MessageBox.Show("时间格式 HH:mm"); return; } EventTime = string.IsNullOrEmpty(t2) ? null : t2; DialogResult = true; Close(); });
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
        save.Click += (_, _) => { var t = tb.Text.Trim(); if (string.IsNullOrEmpty(t)) { t = "日程"; } EventTitle = t; EventDate = dp.SelectedDate?.Date ?? DateTime.Today; var t2 = tm.Text.Trim(); if (!string.IsNullOrEmpty(t2) && !System.Text.RegularExpressions.Regex.IsMatch(t2, @"^\d{2}:\d{2}$")) { MessageBox.Show("时间格式 HH:mm"); return; } EventTime = string.IsNullOrEmpty(t2) ? null : t2; DialogResult = true; Close(); };
        btns.Children.Add(cancel); btns.Children.Add(save); Grid.SetRow(btns, 6); g.Children.Add(btns); Content = g;
    }
    static TextBlock Lbl(string t, int top, int btm, int bot) => new() { Text = t, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C,0x1C,0x1E)), Margin = new Thickness(0, top, btm, bot) };
    static TextBox Tb(string t, int w = 0) => new() { Text = t, FontSize = 14, Padding = new Thickness(8, 6, 8, 6), Width = w > 0 ? w : double.NaN, BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5,0xE5,0xEA)), BorderThickness = new Thickness(1) };
}
