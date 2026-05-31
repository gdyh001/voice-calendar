using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VoiceCalendar.Services;

public class NlpParserService
{
    private static readonly Dictionary<string, int> DateKeywords = new()
    {
        {"今天", 0}, {"今日", 0},
        {"明天", 1}, {"明日", 1},
        {"后天", 2}, {"后日", 2},
        {"大后天", 3},
    };

    private static readonly Dictionary<string, int> WeekdayNames = new()
    {
        {"周一", 0}, {"周二", 1}, {"周三", 2}, {"周四", 3}, {"周五", 4}, {"周六", 5}, {"周日", 6},
        {"星期一", 0}, {"星期二", 1}, {"星期三", 2}, {"星期四", 3}, {"星期五", 4}, {"星期六", 5}, {"星期天", 6},
        {"礼拜一", 0}, {"礼拜二", 1}, {"礼拜三", 2}, {"礼拜四", 3}, {"礼拜五", 4}, {"礼拜六", 5}, {"礼拜天", 6},
    };

    private static readonly Dictionary<string, int> CnNum = new()
    {
        {"一", 1}, {"二", 2}, {"三", 3}, {"四", 4}, {"五", 5},
        {"六", 6}, {"七", 7}, {"八", 8}, {"九", 9}, {"十", 10},
        {"十一", 11}, {"十二", 12}, {"十三", 13}, {"十四", 14},
        {"十五", 15}, {"十六", 16}, {"十七", 17}, {"十八", 18}, {"十九", 19},
        {"二十", 20}, {"二十一", 21}, {"二十二", 22}, {"二十三", 23},
        {"二十四", 24}, {"二十五", 25}, {"二十六", 26}, {"二十七", 27},
        {"三十一", 31}, {"四十", 40}, {"四十一", 41}, {"四十二", 42}, {"四十三", 43},
        {"四十四", 44}, {"四十五", 45}, {"四十六", 46}, {"四十七", 47}, {"四十八", 48}, {"四十九", 49},
        {"五十", 50}, {"五十一", 51}, {"五十二", 52}, {"五十三", 53}, {"五十四", 54}, {"五十五", 55},
        {"五十六", 56}, {"五十七", 57}, {"五十八", 58}, {"五十九", 59},
        {"两", 2}, {"零", 0},
    };

    private static readonly string[] AddKeywords = { "添加", "新增", "增加", "加入", "加一个", "记一个", "记录", "提醒我", "提醒", "新建", "创建" };
    private static readonly string[] DeleteKeywords = { "删除", "删掉", "去掉", "取消", "移除", "清除", "删", "去除" };
    private static readonly string[] QueryKeywords = { "查看", "查询", "看", "显示", "有什么", "什么事", "有哪些", "列出" };

    public class ParseResult
    {
        public string Action { get; set; } = "add";
        public DateTime? Date { get; set; }
        public string? Time { get; set; }
        public int? Hour { get; set; }
        public int? Minute { get; set; }
        public string? EndTime { get; set; }
        public int? EndHour { get; set; }
        public bool IsRecurring { get; set; }
        public string? RecurrenceDays { get; set; }
        public int? EndMinute { get; set; }
        public string Title { get; set; } = "";
        public string Raw { get; set; } = "";
    }

    public ParseResult Parse(string text)
    {
        var today = DateTime.Today;
        var result = new ParseResult { Raw = text };

        result.Action = ParseAction(text);
        var (isRecurring, recurrenceDays) = ParseRecurrence(text);
        result.IsRecurring = isRecurring;
        result.RecurrenceDays = recurrenceDays;
        result.Date = ParseDate(text, today);
        var (startTime, endTime) = ParseTimeRange(text);
        result.Time = startTime;
        result.EndTime = endTime;
        if (!string.IsNullOrEmpty(result.Time) && result.Time.Length == 5)
        {
            if (int.TryParse(result.Time.Substring(0, 2), out int h)) result.Hour = h;
            if (int.TryParse(result.Time.Substring(3, 2), out int m)) result.Minute = m;
        }
        if (!string.IsNullOrEmpty(result.EndTime) && result.EndTime.Length == 5)
        {
            if (int.TryParse(result.EndTime.Substring(0, 2), out int eh)) result.EndHour = eh;
            if (int.TryParse(result.EndTime.Substring(3, 2), out int em)) result.EndMinute = em;
        }
        result.Title = ExtractTitle(text);

        return result;
    }

    private static string ParseAction(string text)
    {
        foreach (var kw in AddKeywords)
            if (text.Contains(kw)) return "add";
        foreach (var kw in DeleteKeywords)
            if (text.Contains(kw)) return "delete";
        foreach (var kw in QueryKeywords)
            if (text.Contains(kw)) return "query";
        return "add";
    }

    // ================================================================
    //  ParseDate — 日期解析
    // ================================================================
    public static DateTime? ParseDate(string text, DateTime? today = null)
    {
        today ??= DateTime.Today;
        var t = today.Value;

        // 1. 相对日期关键词：今天/明天/后天/大后天
        foreach (var (kw, offset) in DateKeywords)
            if (text.Contains(kw))
                return t.AddDays(offset);

        // 2. 下周X
        var nextWeek = Regex.Match(text, @"下周([一二三四五六日天])");
        if (nextWeek.Success)
        {
            var dayKey = "周" + nextWeek.Groups[1].Value;
            if (WeekdayNames.TryGetValue(dayKey, out int wd))
            {
                int daysAhead = (wd - ((int)t.DayOfWeek + 6) % 7 + 7) % 7;
                if (daysAhead == 0) daysAhead = 7;
                return t.AddDays(daysAhead + 7);
            }
        }

        // 3. 本周X
        foreach (var (name, wd) in WeekdayNames)
        {
            if (text.Contains(name))
            {
                int daysAhead = (wd - ((int)t.DayOfWeek + 6) % 7 + 7) % 7;
                if (daysAhead == 0) daysAhead = 7;
                return t.AddDays(daysAhead);
            }
        }


        // 4a. 中文月+数字日：六月2日/五月31号
        var cnMonthDigitDay = Regex.Match(text, @"([一二三四五六七八九十两]{1,3})\s*月\s*(\d{1,2})\s*[日号]");
        if (cnMonthDigitDay.Success)
        {
            var cm = ParseChineseNumber(cnMonthDigitDay.Groups[1].Value);
            int cd = int.Parse(cnMonthDigitDay.Groups[2].Value);
            if (cm >= 1 && cm <= 12 && cd >= 1 && cd <= 31)
            {
                try { var parsed = new DateTime(t.Year, cm.Value, cd); if (parsed < t) parsed = new DateTime(t.Year + 1, cm.Value, cd); return parsed; }
                catch { return null; }
            }
        }

        // 4b. 中文月+中文日：六月二日/五月三十一号
        var cnMonthCnDay = Regex.Match(text, @"([一二三四五六七八九十两]{1,3})\s*月\s*([一二三四五六七八九十两]{1,4})\s*[日号]");
        if (cnMonthCnDay.Success)
        {
            var cm2 = ParseChineseNumber(cnMonthCnDay.Groups[1].Value);
            var cd2 = ParseChineseNumber(cnMonthCnDay.Groups[2].Value);
            if (cm2 >= 1 && cm2 <= 12 && cd2 >= 1 && cd2 <= 31)
            {
                try { var parsed = new DateTime(t.Year, cm2.Value, cd2.Value); if (parsed < t) parsed = new DateTime(t.Year + 1, cm2.Value, cd2.Value); return parsed; }
                catch { return null; }
            }
        }

        // 4c. X月X日 / X月X号（阿拉伯数字）
        var md = Regex.Match(text, @"(\d{1,2})\s*月\s*(\d{1,2})\s*[日号]");
        if (md.Success)
        {
            int m = int.Parse(md.Groups[1].Value);
            int d = int.Parse(md.Groups[2].Value);
            try
            {
                var parsed = new DateTime(t.Year, m, d);
                if (parsed < t) parsed = new DateTime(t.Year + 1, m, d);
                return parsed;
            }
            catch { return null; }
        }

        // 5. 裸阿拉伯数字 X号 / X日（无月份前缀）
        var bareDay = Regex.Match(text, @"(\d{1,2})\s*[日号]");
        if (bareDay.Success)
        {
            int d = int.Parse(bareDay.Groups[1].Value);
            if (d >= 1 && d <= 31)
            {
                int targetMonth = t.Month;
                int targetYear = t.Year;
                if (d < t.Day)
                {
                    targetMonth++;
                    if (targetMonth > 12) { targetMonth = 1; targetYear++; }
                }
                try { return new DateTime(targetYear, targetMonth, d); }
                catch { return null; }
            }
        }

        // 6. 裸中文数字 X号 / X日（如"二十三号""五号"）
        var bareCnDay = Regex.Match(text, @"([一二三四五六七八九十两]{1,4})\s*[日号]");
        if (bareCnDay.Success)
        {
            var cnNum = bareCnDay.Groups[1].Value;
            var day = ParseChineseNumber(cnNum);
            if (day >= 1 && day <= 31)
            {
                int targetMonth = t.Month;
                int targetYear = t.Year;
                if (day < t.Day)
                {
                    targetMonth++;
                    if (targetMonth > 12) { targetMonth = 1; targetYear++; }
                }
                try { return new DateTime(targetYear, targetMonth, day.Value); }
                catch { return null; }
            }
        }

        return null;
    }

    // ================================================================
    //  ParseChineseNumber — 中文数字→整数
    // ================================================================
    private static int? ParseChineseNumber(string text)
    {
        if (CnNum.TryGetValue(text, out int val))
            return val;

        // 动态组合：二十X / 三十X
        if ((text.StartsWith("二十") || text.StartsWith("三十") || text.StartsWith("四十") || text.StartsWith("五十")) && text.Length == 3)
        {
            var prefix = text.Substring(0, 2);
            var suffix = text.Substring(2, 1);
            if (CnNum.TryGetValue(prefix, out int baseVal) &&
                CnNum.TryGetValue(suffix, out int digit) &&
                digit <= 9)
                return baseVal + digit;
        }

        return null;
    }

    // ================================================================

    // ================================================================
    //  ParseTimeRange — 解析时间段（支持"早上7点到晚上五点"）
    // ================================================================
    public static (string? Start, string? End) ParseTimeRange(string text)
    {
        // 查找 "到" 或 "至" 分隔符
        var rangeMatch = Regex.Match(text, @"(.+?)[到至](.+)");
        if (!rangeMatch.Success)
            return (ParseTime(text), null);

        var before = rangeMatch.Groups[1].Value;
        var after = rangeMatch.Groups[2].Value;

        var start = ParseTime(before);
        var end = ParseTime(after);

        // 如果后半段没有时段词，继承前半段的下午/晚上语境
        if (end != null)
        {
            bool beforePm = before.Contains("下午") || before.Contains("晚上");
            bool beforeAm = before.Contains("上午") || before.Contains("凌晨") || before.Contains("早上");
            bool afterPm = after.Contains("下午") || after.Contains("晚上");
            bool afterAm = after.Contains("上午") || after.Contains("凌晨") || after.Contains("早上");

            if (!afterPm && !afterAm && beforePm)
            {
                // 继承下午语境："下午两点到三点" → 14:00-15:00
                int eh = int.Parse(end.Substring(0, 2));
                if (eh < 12) eh += 12;
                end = $"{eh:D2}:{end.Substring(3, 2)}";
            }
        }

        return (start, end);
    }

    //  ParseTime — 时间解析
    // ================================================================
    public static string? ParseTime(string text)
    {
        // HH:MM 格式
        var hhmm = Regex.Match(text, @"(\d{1,2})\s*[:：]\s*(\d{2})");
        if (hhmm.Success)
        {
            int hh = int.Parse(hhmm.Groups[1].Value);
            int m = int.Parse(hhmm.Groups[2].Value);
            if (hh >= 0 && hh <= 23 && m >= 0 && m <= 59)
                return $"{hh:D2}:{m:D2}";
        }

        // X点 / X点半 / X点X分 / X点一刻（含中文数字）
        var timeMatch = Regex.Match(text,
            @"(\d{1,2}|[一二三四五六七八九十两]{1,4})\s*点(?:(半)|(?:(?:(\d{1,2}|[一二三四五六七八九十两]{1,4})\s*分?)?)|(一刻)?)");

        if (!timeMatch.Success) return null;

        int hour = 0;
        var hourStr = timeMatch.Groups[1].Value;
        if (int.TryParse(hourStr, out int h)) hour = h;
        else if (ParseChineseNumber(hourStr) is int ch) hour = ch;
        else return null;

        int minute = 0;
        if (timeMatch.Groups[2].Success) minute = 30;
        else if (timeMatch.Groups[4].Success) minute = 15;
        else if (timeMatch.Groups[3].Success)
        {
            var minStr = timeMatch.Groups[3].Value;
            if (int.TryParse(minStr, out int mm)) minute = mm;
            else if (ParseChineseNumber(minStr) is int cm) minute = cm;
        }

        // 时段判断
        var isAm = Regex.IsMatch(text, @"凌晨|早上|早晨|清晨|天亮");
        if (text.Contains("下午") || text.Contains("晚上"))
        {
            if (hour < 12) hour += 12;
        }
        else if (text.Contains("上午") && hour == 12)
        {
            hour = 0;
        }
        else if (isAm && hour == 12)
        {
            // 凌晨12点 → 00:00
            hour = 0;
        }

        if (hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59)
            return $"{hour:D2}:{minute:D2}";

        return null;
    }

    // ================================================================
    //  ExtractTitle — 提取日程标题
    // ================================================================
    public static string ExtractTitle(string text)
    {
        var title = text;

        // 去掉意图关键词
        foreach (var kw in AddKeywords.Concat(DeleteKeywords).Concat(QueryKeywords))
            title = title.Replace(kw, "");

        // 去掉相对日期关键词
        foreach (var kw in DateKeywords.Keys) title = title.Replace(kw, "");
        foreach (var kw in WeekdayNames.Keys) title = title.Replace(kw, "");

        // 去掉中文月+阿拉伯日：六月2日/五月31号
        title = Regex.Replace(title, @"[一二三四五六七八九十两]{1,3}\s*月\s*\d{1,2}\s*[日号]", "");

        // 去掉中文月+中文日：六月二日/五月三十一号
        title = Regex.Replace(title, @"[一二三四五六七八九十两]{1,3}\s*月\s*[一二三四五六七八九十两]{1,4}\s*[日号]", "");

        // 去掉 X月X日/X号（阿拉伯数字）
        title = Regex.Replace(title, @"\d{1,2}\s*月\s*\d{1,2}\s*[日号]", "");

        // 去掉裸 X号/X日（阿拉伯）
        title = Regex.Replace(title, @"\d{1,2}\s*[日号]", "");

        // 去掉裸中文数字 X号/X日（如"二十三号"）

        // 去掉孤立的中文月份（如"六月"）
        title = Regex.Replace(title, @"[一二三四五六七八九十两]{1,3}\s*月", "");
        title = Regex.Replace(title, @"[一二三四五六七八九十两]{1,4}\s*[日号]", "");

        // 去掉 HH:MM
        title = Regex.Replace(title, @"\d{1,2}\s*[:：]\s*\d{2}", "");

        // 去掉 X点/X点半/X点X分（阿拉伯+中文数字）
        title = Regex.Replace(title, @"(\d{1,2}|[一二三四五六七八九十两]{1,4})\s*点\s*(半|一刻|(\d{1,2}|[一二三四五六七八九十两]{1,4})\s*分?)?", "");

        // 去掉时段词
        title = Regex.Replace(title, @"(上午|下午|晚上|中午|凌晨|早上|早晨|清晨)", "");
        title = Regex.Replace(title, @"每周[一二三四五六日天]", "");
        title = title.Replace("下周", "");
        title = title.Replace("到", "");
        title = title.Replace("至", "");
        title = title.Replace("-", "");

        // 去掉标点
        title = Regex.Replace(title, @"[，,。.!！?？;；、]", "");

        title = title.Trim();
        return string.IsNullOrEmpty(title) ? "未命名日程" : title;
    }

    private static (bool, string?) ParseRecurrence(string text)
    {
        var weeklyMatch = Regex.Match(text, @"每周([一二三四五六日天])");
        if (weeklyMatch.Success)
        {
            var dayMap = new Dictionary<char, int>
            {
                {'一', 0}, {'二', 1}, {'三', 2}, {'四', 3}, {'五', 4}, {'六', 5}, {'日', 6}, {'天', 6}
            };
            var day = weeklyMatch.Groups[1].Value[0];
            if (dayMap.TryGetValue(day, out int idx))
                return (true, idx.ToString());
        }
        return (false, null);
    }
}
